using System.Threading.Channels;

namespace IhsanDev.Shared.Infrastructure.Services.Audit;

internal interface IAuditChannel
{
    void Publish(string connectionString, IReadOnlyList<AuditLogPending> entries);
    ChannelReader<AuditWriteJob> Reader { get; }
}
