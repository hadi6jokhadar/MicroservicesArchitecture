using System.Threading.Channels;

namespace IhsanDev.Shared.Infrastructure.Services.Audit;

internal sealed class AuditChannelService : IAuditChannel
{
    private readonly Channel<AuditWriteJob> _channel = Channel.CreateUnbounded<AuditWriteJob>(
        new UnboundedChannelOptions { SingleReader = true, AllowSynchronousContinuations = false });

    public void Publish(string connectionString, IReadOnlyList<AuditLogPending> entries)
        => _channel.Writer.TryWrite(new AuditWriteJob(connectionString, entries));

    public ChannelReader<AuditWriteJob> Reader => _channel.Reader;
}
