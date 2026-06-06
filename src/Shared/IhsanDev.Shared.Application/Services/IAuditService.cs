using IhsanDev.Shared.Kernel.Entities;

namespace IhsanDev.Shared.Application.Services;

public interface IAuditService
{
    void Record(
        string action,
        string entityType,
        string? entityId = null,
        object? before = null,
        object? after = null);

    IReadOnlyList<AuditLogEntity> Flush();
}
