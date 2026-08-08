using System.Net.Http.Json;
using System.Text.Encodings.Web;
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
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Default encoder escapes every non-ASCII character (Arabic, etc.) as \uXXXX — irrelevant
        // for a JSON REST API body (not HTML), and costly: each escaped Arabic letter becomes 6
        // ASCII characters instead of 1, inflating token counts on every chat/transcription request
        // and making Arabic text harder for the model to read naturally.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
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
        string? pipelineRunId = null,
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
        if (!string.IsNullOrWhiteSpace(pipelineRunId))
        {
            request["pipeline_run_id"] = pipelineRunId;
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
        string? pipelineRunId = null,
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
        if (!string.IsNullOrWhiteSpace(pipelineRunId))
        {
            request["pipelineRunId"] = pipelineRunId;
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/embedding");
        // Explicit JsonOptions here too (not just for its naming policy, which this call doesn't use
        // since keys are set verbatim on the dictionary) — without it, Arabic text in inputText gets
        // \uXXXX-escaped by the default encoder. See the JsonOptions field's own comment.
        httpRequest.Content = JsonContent.Create(request, options: JsonOptions);

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

    public async Task<AiTranscriptionResult> TranscribeAsync(
        string settingsKey,
        int fileId,
        string? tenantId = null,
        string? language = null,
        string? pipelineRunId = null,
        CancellationToken cancellationToken = default)
    {
        tenantId ??= _configuration["MultiTenancy:TenantId"]
            ?? throw new InvalidOperationException(
                "MultiTenancy:TenantId is not configured. " +
                "Nasheed is a single-tenant service - set MultiTenancy:TenantId in appsettings.json.");

        var request = new Dictionary<string, object?>
        {
            ["settings_key"] = settingsKey,
            ["file_ids"] = new[] { fileId },
        };
        if (!string.IsNullOrWhiteSpace(language))
        {
            request["language"] = language;
        }
        if (!string.IsNullOrWhiteSpace(pipelineRunId))
        {
            request["pipeline_run_id"] = pipelineRunId;
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/transcription");
        httpRequest.Content = JsonContent.Create(request, options: JsonOptions);

        httpRequest.Headers.Add("x-tenant-id", tenantId);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "AI transcription request failed with status {StatusCode}. Body: {Body}",
                (int)response.StatusCode,
                errorBody);
            throw new HttpRequestException(
                $"AI transcription request failed with status {(int)response.StatusCode}: {errorBody}",
                null,
                response.StatusCode);
        }

        var result = await response.Content.ReadFromJsonAsync<AiTranscriptionResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("AI service returned null transcription response.");

        return new AiTranscriptionResult
        {
            Text = result.Text,
            Language = result.Language,
            Duration = result.Duration,
            Words = result.Words
                .Select(w => new AiTranscriptionWord { Start = w.Start, End = w.End, Word = w.Word })
                .ToList(),
            Segments = result.Segments
                .Select(s => new AiTranscriptionSegment
                {
                    Start = s.Start,
                    End = s.End,
                    Text = s.Text,
                    AvgLogprob = s.AvgLogprob,
                    NoSpeechProb = s.NoSpeechProb,
                })
                .ToList(),
            NoSpeechProbThreshold = result.NoSpeechProbThreshold,
        };
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

    private sealed class AiTranscriptionResponse
    {
        public string Text { get; set; } = string.Empty;
        public string? Language { get; set; }
        public double? Duration { get; set; }
        public List<AiTranscriptionWordDto> Words { get; set; } = [];
        public List<AiTranscriptionSegmentDto> Segments { get; set; } = [];
        public double? NoSpeechProbThreshold { get; set; }
    }

    private sealed class AiTranscriptionWordDto
    {
        public double Start { get; set; }
        public double End { get; set; }
        public string Word { get; set; } = string.Empty;
    }

    private sealed class AiTranscriptionSegmentDto
    {
        public double Start { get; set; }
        public double End { get; set; }
        public string Text { get; set; } = string.Empty;
        public double? AvgLogprob { get; set; }
        public double? NoSpeechProb { get; set; }
    }
}
