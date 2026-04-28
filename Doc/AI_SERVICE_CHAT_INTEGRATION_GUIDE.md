# AI Service Chat Integration Guide (Service-to-Service)

## Purpose

This guide explains how any .NET microservice in the architecture can call the AI service chat endpoints to perform AI-driven tasks internally, without a user JWT token.

**Always read this file when:**

- A .NET service needs to call the AI for internal processing (e.g. auto-summarize, generate content, classify data)
- Integrating AI chat into a non-AI service
- Troubleshooting 401/403 errors when calling AI endpoints from another service

---

## Overview

The AI service exposes two chat endpoints:

| Endpoint                   | Description                              |
| -------------------------- | ---------------------------------------- |
| `POST /api/v1/chat/stream` | Streaming SSE response (token by token)  |
| `POST /api/v1/chat/single` | Full response returned in a single reply |

For internal service-to-service use, prefer **`/api/v1/chat/single`** — it is simpler and does not require SSE parsing.

**AI service base URL:**

- Development: `http://localhost:5008`

---

## Authentication

The AI service uses the same shared-secret mechanism as all other services.

Include these two headers in every request:

| Header             | Value                                                                     |
| ------------------ | ------------------------------------------------------------------------- |
| `X-Service-Secret` | The shared secret from `ServiceCommunication:SharedSecret` in appsettings |
| `X-Service-Name`   | The calling service name (e.g. `"IdentityService"`)                       |

The AI service validates these headers via `get_current_user_or_service` (built with `ihsandev_shared`).  
Authenticated service requests receive `role = "Service"` and bypass user-level tenant requirement enforcement.

---

## Request Body

Both endpoints accept the same JSON body (`ChatRequest`):

```json
{
  "settings_key": "string (required)",
  "system_prompt_key": "string (required)",
  "messages": [{ "role": "user", "content": "string (required, min 1 char)" }],
  "file_ids": [1, 2],
  "session_id": "uuid (optional)"
}
```

### Fields

| Field               | Type            | Required | Description                                                                                         |
| ------------------- | --------------- | -------- | --------------------------------------------------------------------------------------------------- |
| `settings_key`      | `string`        | **Yes**  | Key of the AI provider settings record (e.g. `"OpenAI-gpt-4o"`). Defines the model and credentials. |
| `system_prompt_key` | `string`        | **Yes**  | Key of the system prompt record that scopes behavior for this task.                                 |
| `messages`          | `ChatMessage[]` | **Yes**  | At least one message with `role` and `content`. For simple tasks, one `user` message is enough.     |
| `file_ids`          | `int[]`         | No       | FileManager file IDs to attach as multimodal context (images, audio, documents).                    |
| `session_id`        | `UUID`          | No       | Existing session UUID to continue. Omit to auto-create a new session.                               |

### Message roles

Valid values: `"system"`, `"user"`, `"assistant"`, `"tool"`  
For service-initiated tasks, always use `"user"` unless you are continuing an existing multi-turn session.

---

## Tenant Context

Tenant context is **optional** for service calls. Behavior:

| Header present?     | Effect                                                                                                   |
| ------------------- | -------------------------------------------------------------------------------------------------------- |
| `x-tenant-id: <id>` | AI service resolves AI settings and system prompts **for that tenant first**, then falls back to global. |
| Header absent       | AI service operates in **global scope** — uses global AI settings and system prompts only.               |

Pass `x-tenant-id` only when the task is tenant-specific and you expect tenant-scoped settings or prompts.

---

## Response (Single Endpoint)

`POST /api/v1/chat/single` returns:

```json
{
  "session_id": "uuid",
  "content": "AI response text",
  "prompt_tokens": 120,
  "completion_tokens": 85,
  "total_tokens": 205
}
```

Use `content` as the AI's output. `session_id` can be stored for follow-up messages in the same conversation.

---

## Step-by-Step Integration (.NET Service)

### 1. Add appsettings configuration

```json
{
  "Services": {
    "AiService": {
      "BaseUrl": "http://localhost:5008",
      "Timeout": 60
    }
  },
  "ServiceCommunication": {
    "Enabled": true,
    "SharedSecret": "your-shared-secret-here",
    "ServiceName": "YourServiceName"
  }
}
```

### 2. Register the HttpClient in `Program.cs`

```csharp
builder.Services.AddHttpClient("AiServiceClient", client =>
{
    var baseUrl = builder.Configuration["Services:AiService:BaseUrl"]
        ?? "http://localhost:5008";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");

    var timeout = builder.Configuration.GetValue<int>("Services:AiService:Timeout", 60);
    client.Timeout = TimeSpan.FromSeconds(timeout);

    var serviceSecret = builder.Configuration["ServiceCommunication:SharedSecret"];
    var serviceName   = builder.Configuration["ServiceCommunication:ServiceName"]
                        ?? builder.Configuration["ApplicationName"]
                        ?? "UnknownService";

    if (!string.IsNullOrEmpty(serviceSecret))
    {
        client.DefaultRequestHeaders.Add("X-Service-Secret", serviceSecret);
        client.DefaultRequestHeaders.Add("X-Service-Name", serviceName);
    }
});
```

### 3. Create the request model

```csharp
public class AiChatMessage
{
    public string Role    { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
}

public class AiChatRequest
{
    public string           SettingsKey      { get; set; } = string.Empty;
    public string           SystemPromptKey  { get; set; } = string.Empty;
    public List<AiChatMessage> Messages      { get; set; } = new();
    public List<int>        FileIds          { get; set; } = new();
    public Guid?            SessionId        { get; set; }
}

public class AiChatResponse
{
    public Guid   SessionId        { get; set; }
    public string Content          { get; set; } = string.Empty;
    public int    PromptTokens     { get; set; }
    public int    CompletionTokens { get; set; }
    public int    TotalTokens      { get; set; }
}
```

### 4. Call the AI service

```csharp
public class MyFeatureService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public MyFeatureService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string> GetAiResponseAsync(
        string userMessage,
        string settingsKey,
        string systemPromptKey,
        string? tenantId    = null,
        List<int>? fileIds  = null,
        CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AiServiceClient");

        var request = new AiChatRequest
        {
            SettingsKey     = settingsKey,
            SystemPromptKey = systemPromptKey,
            Messages        = new List<AiChatMessage>
            {
                new() { Role = "user", Content = userMessage }
            },
            FileIds = fileIds ?? new List<int>(),
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/chat/single")
        {
            Content = JsonContent.Create(request, options: new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            })
        };

        // Pass tenant context only when needed
        if (!string.IsNullOrEmpty(tenantId))
            httpRequest.Headers.Add("x-tenant-id", tenantId);

        using var response = await client.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<AiChatResponse>(
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower },
            ct);

        return result?.Content ?? string.Empty;
    }
}
```

> **Note:** The AI service uses **snake_case** JSON field names (`settings_key`, `system_prompt_key`, `file_ids`).  
> Always set `PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower` when serializing/deserializing.

---

## With File Attachments

To attach files from the FileManager service, pass their integer IDs in `file_ids`.  
The AI service will fetch the file bytes from FileManager internally, encode them, and inject them as multimodal content blocks into the last user message before calling the LLM.

```csharp
var request = new AiChatRequest
{
    SettingsKey     = "OpenAI-gpt-4o",
    SystemPromptKey = "analyze-document",
    Messages        = new List<AiChatMessage>
    {
        new() { Role = "user", Content = "Summarize this document." }
    },
    FileIds = new List<int> { 42, 43 }, // FileManager file IDs
};
```

**Supported file types:** images (vision), audio, and documents (injected as text context).  
The provider must support the media type — the AI service validates this and returns `HTTP 400` if not.

---

## Error Handling

| Status | Meaning                                                                                            |
| ------ | -------------------------------------------------------------------------------------------------- |
| `400`  | Validation error — missing required field, unsupported media type for provider, or empty messages. |
| `401`  | Missing or invalid `X-Service-Secret` header.                                                      |
| `403`  | Service name not in allowed list (if whitelist is configured).                                     |
| `404`  | `settings_key` or `system_prompt_key` not found for the given tenant scope.                        |
| `500`  | Internal error or upstream LLM provider failure.                                                   |

Always check status before reading the response body. LLM provider errors are mapped to HTTP status codes by the AI service.

---

## Prerequisites in the AI Service Database

Before calling the endpoints, ensure these records exist in the AI service database:

1. **AI Provider Settings** — a row in `AiProviderSettings` with the `Key` matching `settings_key`.  
   Created via `POST /api/v1/settings` (admin endpoint).

2. **System Prompt** — a row in `AiSystemPrompts` with the `Key` matching `system_prompt_key`.  
   Created via `POST /api/v1/system-prompts` (admin endpoint).

These can be global (no tenant) or tenant-scoped. Tenant-scoped records are matched first; global records are the fallback.

---

## Quick Reference

```
Endpoint    POST http://localhost:5008/api/v1/chat/single
Auth        X-Service-Secret: <shared-secret>
            X-Service-Name: <your-service-name>
Tenant      x-tenant-id: <tenant-id>   (optional)

Body (snake_case JSON):
{
  "settings_key":     "<required>",
  "system_prompt_key": "<required>",
  "messages": [{ "role": "user", "content": "<required>" }],
  "file_ids": [],        // optional
  "session_id": null     // optional
}
```
