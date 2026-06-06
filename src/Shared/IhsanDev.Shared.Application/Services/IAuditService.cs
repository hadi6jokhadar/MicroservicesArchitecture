namespace IhsanDev.Shared.Application.Services;

public interface IAuditService
{
    void Record(
        string action,
        string entityType,
        string? entityId = null,
        object? before = null,
        object? after = null);

    /// <summary>
    /// Publishes all pending audit entries to the background channel for async persistence.
    /// Pass an empty string for in-memory/test contexts — entries are silently discarded.
    /// </summary>
    void Commit(string connectionString);
}
