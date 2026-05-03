using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nasheed.Application.Interfaces;

namespace Nasheed.Infrastructure.Services;

public class AiApiClientService : IAiApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AiApiClientService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public AiApiClientService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<AiApiClientService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;

        var serviceSecret = configuration["ServiceCommunication:SharedSecret"];
        var serviceName = configuration["ServiceCommunication:ServiceName"] ?? "NasheedService";

        if (!string.IsNullOrEmpty(serviceSecret) && !httpClient.DefaultRequestHeaders.Contains("X-Service-Secret"))
        {
            httpClient.DefaultRequestHeaders.Add("X-Service-Secret", serviceSecret);
            httpClient.DefaultRequestHeaders.Add("X-Service-Name", serviceName);
        }
    }

    public async Task<string> ChatAsync(
        string settingsKey,
        string systemPromptKey,
        string? userMessage = null,
        string? tenantId = null,
        IReadOnlyList<int>? fileIds = null,
        CancellationToken cancellationToken = default)
    {
        tenantId ??= _configuration["MultiTenancy:TenantId"]
            ?? throw new InvalidOperationException(
                "MultiTenancy:TenantId is not configured. " +
                "Nasheed is a single-tenant service - set MultiTenancy:TenantId in appsettings.json.");

        var request = new Dictionary<string, object?>
        {
            ["settings_key"] = settingsKey,
            ["system_prompt_key"] = systemPromptKey
        };
        if (!string.IsNullOrWhiteSpace(userMessage))
        {
            request["messages"] = new[] { new { role = "user", content = userMessage } };
        }
        if (fileIds is { Count: > 0 })
        {
            request["file_ids"] = fileIds;
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/chat/single");
        httpRequest.Content = JsonContent.Create(request, options: JsonOptions);

        httpRequest.Headers.Add("x-tenant-id", tenantId);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<AiChatResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("AI service returned null response.");

        return result.Content;
    }

    public async Task<float[]> EmbedAsync(
        string settingsKey,
        string inputText,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        tenantId ??= _configuration["MultiTenancy:TenantId"]
            ?? throw new InvalidOperationException(
                "MultiTenancy:TenantId is not configured. " +
                "Nasheed is a single-tenant service - set MultiTenancy:TenantId in appsettings.json.");

        // Embedding endpoint expects camelCase keys: settingsKey and text.
        var request = new Dictionary<string, object?>
        {
            ["settingsKey"] = settingsKey,
            ["text"] = inputText,
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/embedding");
        httpRequest.Content = JsonContent.Create(request);

        httpRequest.Headers.Add("x-tenant-id", tenantId);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "AI embedding request failed with status {StatusCode}. Body: {Body}",
                (int)response.StatusCode,
                errorBody);
            throw new HttpRequestException(
                $"AI embedding request failed with status {(int)response.StatusCode}: {errorBody}",
                null,
                response.StatusCode);
        }

        var result = await response.Content.ReadFromJsonAsync<AiEmbedResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("AI service returned null embedding response.");

        return result.Embedding;
    }

    private sealed class AiChatResponse
    {
        public Guid SessionId { get; set; }
        public string Content { get; set; } = string.Empty;
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
    }

    private sealed class AiEmbedResponse
    {
        public float[] Embedding { get; set; } = Array.Empty<float>();
        public string ModelKey { get; set; } = string.Empty;
    }
}
