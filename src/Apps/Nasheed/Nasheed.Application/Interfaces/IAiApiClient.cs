namespace Nasheed.Application.Interfaces;

/// <summary>
/// Contract for calling AI.API from the Nasheed service.
/// Implementations live in Infrastructure.
/// </summary>
public interface IAiApiClient
{
    /// <summary>
    /// Sends a chat request to AI.API with a given settings key and system prompt key.
    /// Returns the assistant's text response.
    /// </summary>
    Task<string> ChatAsync(
        string settingsKey,
        string systemPromptKey,
        string? userMessage = null,
        string? tenantId = null,
        IReadOnlyList<int>? fileIds = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates an embedding vector for the given input text using the specified AI settings key.
    /// Returns the embedding as a float array.
    /// </summary>
    Task<float[]> EmbedAsync(
        string settingsKey,
        string inputText,
        string? tenantId = null,
        CancellationToken cancellationToken = default);
}
