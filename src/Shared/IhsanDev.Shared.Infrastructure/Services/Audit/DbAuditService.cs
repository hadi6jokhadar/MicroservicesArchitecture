using IhsanDev.Shared.Application.Services;
using IhsanDev.Shared.Infrastructure.Services.Identity;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using Microsoft.AspNetCore.Http;

namespace IhsanDev.Shared.Infrastructure.Services.Audit;

internal sealed class DbAuditService : IAuditService
{
    private readonly List<AuditLogPending> _pending = [];
    private readonly IAuditChannel _channel;
    private readonly ICurrentUserService? _currentUser;
    private readonly ITenantContext? _tenantContext;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public DbAuditService(
        IAuditChannel channel,
        ICurrentUserService? currentUser = null,
        ITenantContext? tenantContext = null,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _channel = channel;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public void Record(
        string action,
        string entityType,
        string? entityId = null,
        object? before = null,
        object? after = null)
    {
        _pending.Add(new AuditLogPending(
            Action: action,
            EntityType: entityType,
            EntityId: entityId,
            TenantId: _tenantContext?.TenantId,
            UserId: _currentUser?.UserId,
            UserEmail: _currentUser?.Email,
            IpAddress: _httpContextAccessor?.HttpContext?.Connection?.RemoteIpAddress?.ToString(),
            OccurredAt: DateTimeOffset.UtcNow,
            Before: before,
            After: after));
    }

    public void Commit(string connectionString)
    {
        if (_pending.Count == 0) return;
        var rows = _pending.ToList();
        _pending.Clear();

        if (!string.IsNullOrEmpty(connectionString))
            _channel.Publish(connectionString, rows);
        // empty string = in-memory/test context — silently discard
    }
}
