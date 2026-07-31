using System.Globalization;
using Backup.Domain.Entities;

namespace Backup.Application.DTOs;

/// <summary>
/// Read-model for a single restore execution. Manual mapping only — no AutoMapper.
/// Enums are rendered as their string name (not the raw int) for readability in JSON.
/// </summary>
public class RestoreRunDto
{
    public int Id { get; set; }

    public int BackupRunId { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? StartedAt { get; set; }

    public string? CompletedAt { get; set; }

    /// <summary>
    /// Host/database only, never the full connection string (which may carry a cleartext
    /// password in <see cref="RestoreRunEntity.TargetConnectionOverride"/>) — that raw value is
    /// only ever read back from the DB entity by <c>RunRestoreJob</c>, never serialized here.
    /// Null when the restore used the original target (no override).
    /// </summary>
    public string? TargetOverrideSummary { get; set; }

    public int? TriggeredByUserId { get; set; }

    public string? TriggeredByEmail { get; set; }

    public string? ErrorMessage { get; set; }

    public string Created { get; set; } = string.Empty;

    public static RestoreRunDto MapFrom(RestoreRunEntity entity) => new()
    {
        Id = entity.Id,
        BackupRunId = entity.BackupRunId,
        Status = entity.Status.ToString(),
        StartedAt = entity.StartedAt?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
        CompletedAt = entity.CompletedAt?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
        TargetOverrideSummary = RedactConnectionString(entity.TargetConnectionOverride),
        TriggeredByUserId = entity.TriggeredByUserId,
        TriggeredByEmail = entity.TriggeredByEmail,
        ErrorMessage = entity.ErrorMessage,
        Created = entity.Created.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
    };

    private static string? RedactConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        string? host = null;
        string? database = null;

        foreach (var segment in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = segment.IndexOf('=');
            if (separatorIndex < 0)
            {
                continue;
            }

            var key = segment[..separatorIndex].Trim().ToLowerInvariant();
            var value = segment[(separatorIndex + 1)..].Trim();

            if (key is "host" or "server")
            {
                host = value;
            }
            else if (key is "database" or "initial catalog")
            {
                database = value;
            }
        }

        return $"{host ?? "?"}/{database ?? "?"}";
    }
}
