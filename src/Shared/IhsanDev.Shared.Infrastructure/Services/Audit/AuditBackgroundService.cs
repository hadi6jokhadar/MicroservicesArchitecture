using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace IhsanDev.Shared.Infrastructure.Services.Audit;

internal sealed class AuditBackgroundService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IAuditChannel _channel;
    private readonly ILogger<AuditBackgroundService> _logger;

    public AuditBackgroundService(IAuditChannel channel, ILogger<AuditBackgroundService> logger)
    {
        _channel = channel;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var job in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                await ProcessBatchAsync(job, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Drain remaining entries on graceful shutdown
            while (_channel.Reader.TryRead(out var remaining))
            {
                try { await ProcessBatchAsync(remaining, CancellationToken.None); }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to flush {Count} audit entries during shutdown", remaining.Entries.Count);
                }
            }
        }
    }

    private async Task ProcessBatchAsync(AuditWriteJob first, CancellationToken ct)
    {
        // Drain any immediately available jobs to batch by connection string
        var byConnection = new Dictionary<string, List<AuditLogPending>>
        {
            [first.ConnectionString] = [.. first.Entries]
        };

        while (_channel.Reader.TryRead(out var next))
        {
            if (!byConnection.TryGetValue(next.ConnectionString, out var list))
                byConnection[next.ConnectionString] = list = [];
            list.AddRange(next.Entries);
        }

        foreach (var (connectionString, entries) in byConnection)
        {
            try
            {
                await PersistAsync(connectionString, entries, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist {Count} audit entries", entries.Count);
            }
        }
    }

    private static async Task PersistAsync(string connectionString, List<AuditLogPending> entries, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        await using var writer = await conn.BeginBinaryImportAsync(
            """
            COPY "AuditLogs" (
                "Action","EntityType","EntityId","TenantId","UserId",
                "UserEmail","Before","After","IpAddress","OccurredAt"
            ) FROM STDIN (FORMAT BINARY)
            """, ct);

        foreach (var e in entries)
        {
            await writer.StartRowAsync(ct);

            await writer.WriteAsync(e.Action, NpgsqlDbType.Text, ct);
            await writer.WriteAsync(e.EntityType, NpgsqlDbType.Text, ct);

            await WriteNullableText(writer, e.EntityId, ct);
            await WriteNullableText(writer, e.TenantId, ct);
            await WriteNullableText(writer, e.UserId, ct);
            await WriteNullableText(writer, e.UserEmail, ct);

            // JSON serialization happens here — off the request hot path
            var before = e.Before is null ? null : JsonSerializer.Serialize(e.Before, JsonOptions);
            var after  = e.After  is null ? null : JsonSerializer.Serialize(e.After,  JsonOptions);
            await WriteNullableText(writer, before, ct);
            await WriteNullableText(writer, after, ct);

            await WriteNullableText(writer, e.IpAddress, ct);
            await writer.WriteAsync(e.OccurredAt, NpgsqlDbType.TimestampTz, ct);
        }

        await writer.CompleteAsync(ct);
    }

    private static async Task WriteNullableText(NpgsqlBinaryImporter writer, string? value, CancellationToken ct)
    {
        if (value is null)
            await writer.WriteNullAsync(ct);
        else
            await writer.WriteAsync(value, NpgsqlDbType.Text, ct);
    }
}
