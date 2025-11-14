using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Notification.Application.DTOs;
using Notification.Application.Interfaces;

namespace Notification.Infrastructure.Services;

/// <summary>
/// Implementation of Firebase Cloud Messaging service
/// </summary>
public class FirebaseService : IFirebaseService
{
    private readonly ILogger<FirebaseService> _logger;
    private readonly bool _isEnabled;
    private readonly FirebaseApp? _firebaseApp;

    public FirebaseService(
        IConfiguration configuration,
        ILogger<FirebaseService> logger)
    {
        _logger = logger;
        _isEnabled = configuration.GetValue<bool>("Firebase:Enabled", false);

        if (_isEnabled)
        {
            try
            {
                var serviceAccountKeyPath = configuration.GetValue<string>("Firebase:ServiceAccountKeyPath");
                var projectId = configuration.GetValue<string>("Firebase:ProjectId");

                if (string.IsNullOrWhiteSpace(serviceAccountKeyPath))
                {
                    _logger.LogError("Firebase:ServiceAccountKeyPath is not configured");
                    _isEnabled = false;
                    return;
                }

                if (string.IsNullOrWhiteSpace(projectId))
                {
                    _logger.LogError("Firebase:ProjectId is not configured");
                    _isEnabled = false;
                    return;
                }

                // Check if Firebase app already exists
                if (FirebaseApp.DefaultInstance == null)
                {
                    _firebaseApp = FirebaseApp.Create(new AppOptions
                    {
                        Credential = GoogleCredential.FromFile(serviceAccountKeyPath),
                        ProjectId = projectId
                    });

                    _logger.LogInformation(
                        "Firebase initialized successfully for project: {ProjectId}",
                        projectId);
                }
                else
                {
                    _firebaseApp = FirebaseApp.DefaultInstance;
                    _logger.LogInformation("Using existing Firebase app instance");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Firebase. Push notifications will be disabled.");
                _isEnabled = false;
            }
        }
        else
        {
            _logger.LogInformation("Firebase is disabled in configuration");
        }
    }

    public bool IsEnabled => _isEnabled;

    public async Task<bool> SendToDeviceAsync(
        string deviceToken,
        string title,
        string message,
        Dictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
    {
        if (!_isEnabled || _firebaseApp == null)
        {
            _logger.LogWarning("Firebase is not enabled or not initialized");
            return false;
        }

        try
        {
            var messageBuilder = new Message
            {
                Token = deviceToken,
                Notification = new FirebaseAdmin.Messaging.Notification
                {
                    Title = title,
                    Body = message
                },
                Data = data
            };

            var response = await FirebaseMessaging.DefaultInstance.SendAsync(messageBuilder, cancellationToken);

            _logger.LogInformation(
                "Firebase notification sent successfully. MessageId: {MessageId}, Token: {Token}",
                response,
                MaskToken(deviceToken));

            return true;
        }
        catch (FirebaseMessagingException ex) when (
            ex.MessagingErrorCode == MessagingErrorCode.Unregistered ||
            ex.MessagingErrorCode == MessagingErrorCode.InvalidArgument)
        {
            _logger.LogWarning(
                ex,
                "Invalid or unregistered device token: {Token}. Error: {ErrorCode}",
                MaskToken(deviceToken),
                ex.MessagingErrorCode);

            // Return false to indicate token should be removed
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send Firebase notification to token: {Token}",
                MaskToken(deviceToken));

            return false;
        }
    }

    public async Task<FirebaseMulticastResult> SendToMultipleDevicesAsync(
        List<string> deviceTokens,
        string title,
        string message,
        Dictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
    {
        var result = new FirebaseMulticastResult();

        if (!_isEnabled || _firebaseApp == null)
        {
            _logger.LogWarning("Firebase is not enabled or not initialized");
            result.FailureCount = deviceTokens.Count;
            foreach (var token in deviceTokens)
            {
                result.TokenResults[token] = false;
            }
            return result;
        }

        if (!deviceTokens.Any())
        {
            _logger.LogWarning("No device tokens provided for multicast message");
            return result;
        }

        try
        {
            // Firebase has a 500 token limit per multicast request
            const int FIREBASE_MAX_BATCH_SIZE = 500;

            // Process tokens in batches of 500
            for (int batchIndex = 0; batchIndex < deviceTokens.Count; batchIndex += FIREBASE_MAX_BATCH_SIZE)
            {
                var batch = deviceTokens.Skip(batchIndex).Take(FIREBASE_MAX_BATCH_SIZE).ToList();

                var multicastMessage = new MulticastMessage
                {
                    Tokens = batch,
                    Notification = new FirebaseAdmin.Messaging.Notification
                    {
                        Title = title,
                        Body = message
                    },
                    Data = data
                };

                var response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(
                    multicastMessage,
                    cancellationToken);

                result.SuccessCount += response.SuccessCount;
                result.FailureCount += response.FailureCount;

                // Map results back to tokens for this batch
                for (int i = 0; i < batch.Count; i++)
                {
                    var token = batch[i];
                    var sendResponse = response.Responses[i];

                    if (sendResponse.IsSuccess)
                    {
                        result.TokenResults[token] = true;
                        _logger.LogDebug(
                            "Firebase notification sent successfully to token: {Token}, MessageId: {MessageId}",
                            MaskToken(token),
                            sendResponse.MessageId);
                    }
                    else
                    {
                        result.TokenResults[token] = false;

                        var exception = sendResponse.Exception;
                        if (exception is FirebaseMessagingException messagingEx)
                        {
                            if (messagingEx.MessagingErrorCode == MessagingErrorCode.Unregistered ||
                                messagingEx.MessagingErrorCode == MessagingErrorCode.InvalidArgument)
                            {
                                // Mark token for deletion
                                result.InvalidTokenIds.Add(token);
                                
                                _logger.LogWarning(
                                    "Invalid or unregistered device token: {Token}. Error: {ErrorCode}",
                                    MaskToken(token),
                                    messagingEx.MessagingErrorCode);
                            }
                            else
                            {
                                _logger.LogError(
                                    exception,
                                    "Failed to send Firebase notification to token: {Token}. Error: {ErrorCode}",
                                    MaskToken(token),
                                    messagingEx.MessagingErrorCode);
                            }
                        }
                        else
                        {
                            _logger.LogError(
                                exception,
                                "Failed to send Firebase notification to token: {Token}",
                                MaskToken(token));
                        }
                    }
                }
            }

            _logger.LogInformation(
                "Firebase multicast completed. Total Success: {SuccessCount}, Total Failure: {FailureCount}, Batches: {BatchCount}",
                result.SuccessCount,
                result.FailureCount,
                (deviceTokens.Count + FIREBASE_MAX_BATCH_SIZE - 1) / FIREBASE_MAX_BATCH_SIZE);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send multicast Firebase notification");

            result.FailureCount = deviceTokens.Count;
            foreach (var token in deviceTokens)
            {
                result.TokenResults[token] = false;
            }

            return result;
        }
    }

    /// <summary>
    /// Mask device token for logging (show first 8 and last 4 characters)
    /// </summary>
    private string MaskToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length <= 12)
        {
            return "***";
        }

        return $"{token[..8]}...{token[^4..]}";
    }
}
