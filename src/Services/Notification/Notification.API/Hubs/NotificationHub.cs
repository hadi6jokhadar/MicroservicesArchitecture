using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using MediatR;
using Notification.Application.Commands;
using System.Security.Claims;

namespace Notification.API.Hubs;

/// <summary>
/// SignalR Hub for real-time notification delivery
/// Supports both authenticated and anonymous connections
/// Supports multi-tenancy and global notifications
/// </summary>
public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;
    private readonly IMediator _mediator;
    private readonly IConfiguration _configuration;
    private readonly bool _isMultiTenancyEnabled;

    public NotificationHub(
        ILogger<NotificationHub> logger,
        IMediator mediator,
        IConfiguration configuration)
    {
        _logger = logger;
        _mediator = mediator;
        _configuration = configuration;
        _isMultiTenancyEnabled = _configuration.GetValue<bool>("MultiTenancy:Enabled", true);
    }

    /// <summary>
    /// Called when a client connects to the hub
    /// Supports both authenticated and anonymous connections
    /// Logic:
    /// 1. No tenant + no token => global notifications only
    /// 2. Tenant + no token => global + tenant notifications
    /// 3. Token (must have tenant if MultiTenancy:Enabled=true) => all notifications
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        try
        {
            var httpContext = Context.GetHttpContext();
            
            // Get tenant ID from header OR query string
            var tenantId = httpContext?.Request.Headers["x-tenant-id"].FirstOrDefault()
                ?? httpContext?.Request.Query["tenantId"].FirstOrDefault();
            
            // Get authenticated user ID from claims (optional)
            var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier);
            var userId = userIdClaim?.Value;
            var isAuthenticated = !string.IsNullOrEmpty(userId);

            // Always add to global group (for global broadcasts)
            await Groups.AddToGroupAsync(Context.ConnectionId, "global");

            if (_isMultiTenancyEnabled)
            {
                // Multi-tenancy mode enabled
                if (isAuthenticated)
                {
                    // Token provided - must have tenant (rule 3)
                    if (!string.IsNullOrWhiteSpace(tenantId))
                    {
                        // Add to tenant-wide group (for tenant broadcasts)
                        await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant:{tenantId}");
                        
                        // Add to tenant-user specific group
                        await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant:{tenantId}:user:{userId}");
                        
                        _logger.LogInformation(
                            "User {UserId} connected to tenant {TenantId}. ConnectionId: {ConnectionId}",
                            userId,
                            tenantId,
                            Context.ConnectionId);
                    }
                    else
                    {
                        // Token without tenant - not allowed in multi-tenancy mode
                        _logger.LogWarning(
                            "Authenticated user {UserId} connected without tenant ID in multi-tenancy mode. ConnectionId: {ConnectionId}. Only global notifications will be received.",
                            userId,
                            Context.ConnectionId);
                    }
                }
                else
                {
                    // No token - anonymous connection (rule 1 or 2)
                    if (!string.IsNullOrWhiteSpace(tenantId))
                    {
                        // Tenant without token - rule 2: global + tenant notifications
                        await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant:{tenantId}");
                        
                        _logger.LogInformation(
                            "Anonymous user connected to tenant {TenantId}. ConnectionId: {ConnectionId}",
                            tenantId,
                            Context.ConnectionId);
                    }
                    else
                    {
                        // No tenant and no token - rule 1: global notifications only
                        _logger.LogInformation(
                            "Anonymous user connected (global only). ConnectionId: {ConnectionId}",
                            Context.ConnectionId);
                    }
                }
            }
            else
            {
                // Single-tenant mode (MultiTenancy:Enabled = false)
                // Tenant ID is not relevant, token is optional
                await Groups.AddToGroupAsync(Context.ConnectionId, "all-clients");

                if (isAuthenticated)
                {
                    // Add to user-specific group
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
                    
                    _logger.LogInformation(
                        "User {UserId} connected (single-tenant mode). ConnectionId: {ConnectionId}",
                        userId,
                        Context.ConnectionId);
                }
                else
                {
                    // Anonymous user (receives global and all-clients broadcasts)
                    _logger.LogInformation(
                        "Anonymous user connected (single-tenant mode). ConnectionId: {ConnectionId}",
                        Context.ConnectionId);
                }
            }

            await base.OnConnectedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnConnectedAsync for ConnectionId: {ConnectionId}", Context.ConnectionId);
            Context.Abort();
        }
    }

    /// <summary>
    /// Called when a client disconnects from the hub
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier);
        var userId = userIdClaim?.Value ?? "unknown";

        if (exception != null)
        {
            _logger.LogWarning(
                exception,
                "User {UserId} disconnected with error. ConnectionId: {ConnectionId}",
                userId,
                Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation(
                "User {UserId} disconnected. ConnectionId: {ConnectionId}",
                userId,
                Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Client-to-server method to acknowledge notification delivery
    /// </summary>
    /// <param name="queueItemId">Queue item ID to acknowledge</param>
    public async Task AcknowledgeDelivery(int queueItemId)
    {
        try
        {
            var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                _logger.LogWarning(
                    "Acknowledge delivery failed: User not authenticated. ConnectionId: {ConnectionId}",
                    Context.ConnectionId);
                return;
            }

            var userId = userIdClaim.Value;

            var command = new AcknowledgeNotificationCommand(
                QueueItemId: queueItemId,
                ConnectionId: Context.ConnectionId,
                ReceivedAt: DateTime.UtcNow
            );

            var success = await _mediator.Send(command);

            if (success)
            {
                _logger.LogInformation(
                    "Notification acknowledged: {QueueItemId} by User: {UserId}, ConnectionId: {ConnectionId}",
                    queueItemId,
                    userId,
                    Context.ConnectionId);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to acknowledge notification: {QueueItemId} by User: {UserId}",
                    queueItemId,
                    userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error acknowledging notification: {QueueItemId}, ConnectionId: {ConnectionId}",
                queueItemId,
                Context.ConnectionId);
        }
    }

    /// <summary>
    /// Send global notification to ALL clients (authenticated and anonymous)
    /// This reaches every single connection regardless of tenant or user
    /// </summary>
    public async Task SendGlobalNotification(object notification)
    {
        await Clients.Group("global").SendAsync("ReceiveNotification", notification);
        
        _logger.LogInformation("Global notification sent to all clients");
    }

    /// <summary>
    /// Send notification to all clients (when MultiTenancy:Enabled = false)
    /// Same as global but semantically different for single-tenant mode
    /// </summary>
    public async Task SendToAllClients(object notification)
    {
        if (_isMultiTenancyEnabled)
        {
            _logger.LogWarning("SendToAllClients called but multi-tenancy is enabled. Use SendGlobalNotification or SendToTenant instead.");
            return;
        }

        await Clients.Group("all-clients").SendAsync("ReceiveNotification", notification);
        
        _logger.LogInformation("Notification sent to all clients (single-tenant mode)");
    }

    /// <summary>
    /// Send notification to all clients in a specific tenant (MultiTenancy:Enabled = true)
    /// Includes both authenticated and anonymous users in the tenant
    /// </summary>
    /// <param name="tenantId">Tenant identifier</param>
    /// <param name="notification">Notification payload</param>
    public async Task SendToTenant(string tenantId, object notification)
    {
        if (!_isMultiTenancyEnabled)
        {
            _logger.LogWarning("SendToTenant called but multi-tenancy is disabled. Use SendToAllClients instead.");
            return;
        }

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            _logger.LogWarning("SendToTenant called with empty tenantId");
            return;
        }

        var groupName = $"tenant:{tenantId}";
        await Clients.Group(groupName).SendAsync("ReceiveNotification", notification);

        _logger.LogInformation(
            "Notification sent to all clients in tenant: {TenantId}",
            tenantId);
    }

    /// <summary>
    /// Send notification to a specific user in a tenant (MultiTenancy:Enabled = true)
    /// </summary>
    /// <param name="tenantId">Tenant identifier</param>
    /// <param name="userId">User identifier</param>
    /// <param name="notification">Notification payload</param>
    public async Task SendToUserInTenant(string tenantId, string userId, object notification)
    {
        if (!_isMultiTenancyEnabled)
        {
            _logger.LogWarning("SendToUserInTenant called but multi-tenancy is disabled. Use SendToUser instead.");
            return;
        }

        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("SendToUserInTenant called with empty tenantId or userId");
            return;
        }

        var groupName = $"tenant:{tenantId}:user:{userId}";
        await Clients.Group(groupName).SendAsync("ReceiveNotification", notification);

        _logger.LogInformation(
            "Notification sent to user {UserId} in tenant {TenantId}",
            userId,
            tenantId);
    }

    /// <summary>
    /// Send notification to a specific user (MultiTenancy:Enabled = false)
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="notification">Notification payload</param>
    public async Task SendToUser(string userId, object notification)
    {
        if (_isMultiTenancyEnabled)
        {
            _logger.LogWarning("SendToUser called but multi-tenancy is enabled. Use SendToUserInTenant instead.");
            return;
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("SendToUser called with empty userId");
            return;
        }

        var groupName = $"user:{userId}";
        await Clients.Group(groupName).SendAsync("ReceiveNotification", notification);

        _logger.LogInformation(
            "Notification sent to user {UserId} (single-tenant mode)",
            userId);
    }
}
