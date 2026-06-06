namespace IhsanDev.Shared.Infrastructure.Services.Audit;

internal sealed record AuditWriteJob(
    string ConnectionString,
    IReadOnlyList<AuditLogPending> Entries);
