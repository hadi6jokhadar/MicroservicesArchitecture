using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IhsanDev.Shared.Infrastructure.Services.Notification;

/// <summary>
/// Client for communicating with the Notification Service
/// Handles service-to-service authentication automatically
/// </summary>
public class NotificationServiceClient : INotificationServiceClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationServiceClient> _logger;

    public NotificationServiceClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<NotificationServiceClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendNotificationAsync(
        string tenantId,
        int userId,
        string title,
        string message,
        string? data = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("NotificationService");

            // Create notification payload
            var payload = new
            {
                tenantId = tenantId,
                userId = userId,
                title = title,
                message = message,
                data = data,
                deliveryType = "Both",      // SignalR + Firebase
                priority = "Immediate"      // Process ASAP
            };

            // Prepare request with tenant header
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/notifications/send");
            request.Headers.Add("x-tenant-id", tenantId);
            request.Content = JsonContent.Create(payload);

            // Send request (service authentication headers already added in HttpClient configuration)
            var response = await client.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Notification sent successfully to user {UserId} in tenant {TenantId}: {Title}",
                    userId, tenantId, title);
                return true;
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Failed to send notification. Status: {Status}, Error: {Error}",
                    response.StatusCode, error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error sending notification to user {UserId} in tenant {TenantId}",
                userId, tenantId);
            return false;
        }
    }

    public async Task<bool> SendTenantBroadcastAsync(
        string tenantId,
        string title,
        string message,
        string? data = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("NotificationService");

            // Create notification payload (no userId = broadcast)
            var payload = new
            {
                tenantId = tenantId,
                userId = (int?)null,  // Null = broadcast to all users in tenant
                title = title,
                message = message,
                data = data,
                deliveryType = "Both",
                priority = "Immediate"
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/notifications/send");
            request.Headers.Add("x-tenant-id", tenantId);
            request.Content = JsonContent.Create(payload);

            var response = await client.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Broadcast notification sent successfully to tenant {TenantId}: {Title}",
                    tenantId, title);
                return true;
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Failed to send tenant broadcast. Status: {Status}, Error: {Error}",
                    response.StatusCode, error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error sending broadcast notification to tenant {TenantId}",
                tenantId);
            return false;
        }
    }

    public async Task<bool> SendGlobalNotificationAsync(
        string title,
        string message,
        string? data = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("NotificationService");

            // Create notification payload (no tenantId and no userId = global)
            var payload = new
            {
                tenantId = (string?)null,  // Null = global notification
                userId = (int?)null,       // Null = all users
                title = title,
                message = message,
                data = data,
                deliveryType = "Both",
                priority = "Immediate"
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/notifications/send");
            request.Content = JsonContent.Create(payload);

            var response = await client.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Global notification sent successfully: {Title}",
                    title);
                return true;
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Failed to send global notification. Status: {Status}, Error: {Error}",
                    response.StatusCode, error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending global notification");
            return false;
        }
    }
}
