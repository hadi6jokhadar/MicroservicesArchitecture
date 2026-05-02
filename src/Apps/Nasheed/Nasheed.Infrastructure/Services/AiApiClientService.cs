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
        string userMessage,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            settings_key = settingsKey,
            system_prompt_key = systemPromptKey,
            messages = new[] { new { role = "user", content = userMessage } }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/chat/single");
        httpRequest.Content = JsonContent.Create(request, options: JsonOptions);

        if (!string.IsNullOrEmpty(tenantId))
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
        var request = new
        {
            settings_key = settingsKey,
            input = inputText
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/embed");
        httpRequest.Content = JsonContent.Create(request, options: JsonOptions);

        if (!string.IsNullOrEmpty(tenantId))
            httpRequest.Headers.Add("x-tenant-id", tenantId);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

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
