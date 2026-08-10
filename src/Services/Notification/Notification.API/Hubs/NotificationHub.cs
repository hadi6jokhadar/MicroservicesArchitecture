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

            // Get authenticated user ID from claims (optional)
            var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier);
            var userId = userIdClaim?.Value;
            var isAuthenticated = !string.IsNullOrEmpty(userId);

            // An authenticated connection is pinned to the tenant baked into its OWN JWT
            // (the "tenant_id" claim set by Identity's UserService.GenerateTokensAsync) — never
            // the client-supplied header/query value. This hub is registered with
            // [OptionalTenant], which makes JwtTenantVerificationMiddleware skip its usual
            // JWT-vs-header mismatch check entirely for this route; reading the raw header/query
            // here instead of the validated claim let any authenticated (or anonymous) client
            // join another tenant's "tenant:{tenantId}" broadcast group just by connecting with
            // ?tenantId=<victim-tenant> — a cross-tenant notification leak. A JWT with no
            // tenant_id claim means a cross-tenant SuperAdmin/Service account (Shared JWT mode),
            // which legitimately picks its tenant via the client-supplied value, same as an
            // anonymous connection.
            var jwtTenantId = Context.User?.FindFirst("tenant_id")?.Value;
            var tenantId = isAuthenticated && !string.IsNullOrWhiteSpace(jwtTenantId)
                ? jwtTenantId
                : httpContext?.Request.Headers["x-tenant-id"].FirstOrDefault()
                    ?? httpContext?.Request.Query["tenantId"].FirstOrDefault();

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
            // int.TryParse failing (unparseable claim) intentionally falls through as 0, which
            // matches no real NotificationQueueItem.UserId — fail closed, not open.
            int.TryParse(userId, out var requestingUserId);

            var command = new AcknowledgeNotificationCommand(
                QueueItemId: queueItemId,
                ConnectionId: Context.ConnectionId,
                ReceivedAt: DateTime.UtcNow,
                RequestingUserId: requestingUserId
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

    // SendGlobalNotification, SendToAllClients, SendToTenant, SendToUserInTenant, and SendToUser
    // were removed (July 2026 security audit): they were public hub RPCs with no auth/ownership
    // check, letting any anonymous connection broadcast attacker-supplied content to any
    // tenant/user/everyone. Confirmed unused by every client (frontend and otherwise) — the only
    // legitimate delivery path is NotificationProcessor pushing via IHubContext directly, which
    // does not call through the hub's own methods.
}
