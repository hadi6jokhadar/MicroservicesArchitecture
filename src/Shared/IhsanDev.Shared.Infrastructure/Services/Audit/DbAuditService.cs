using System.Text.Json;
using IhsanDev.Shared.Application.Services;
using IhsanDev.Shared.Infrastructure.Services.Identity;
using IhsanDev.Shared.Kernel.Entities;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using Microsoft.AspNetCore.Http;

namespace IhsanDev.Shared.Infrastructure.Services.Audit;

public sealed class DbAuditService : IAuditService
{
    private readonly List<AuditLogEntity> _pending = [];
    private readonly ICurrentUserService? _currentUser;
    private readonly ITenantContext? _tenantContext;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public DbAuditService(
        ICurrentUserService? currentUser = null,
        ITenantContext? tenantContext = null,
        IHttpContextAccessor? httpContextAccessor = null)
    {
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
        _pending.Add(new AuditLogEntity
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            TenantId = _tenantContext?.TenantId,
            UserId = _currentUser?.UserId,
            UserEmail = _currentUser?.Email,
            Before = before is null ? null : JsonSerializer.Serialize(before),
            After = after is null ? null : JsonSerializer.Serialize(after),
            IpAddress = _httpContextAccessor?.HttpContext?.Connection?.RemoteIpAddress?.ToString(),
            OccurredAt = DateTimeOffset.UtcNow
        });
    }

    public IReadOnlyList<AuditLogEntity> Flush()
    {
        var rows = _pending.ToList();
        _pending.Clear();
        return rows;
    }
}
