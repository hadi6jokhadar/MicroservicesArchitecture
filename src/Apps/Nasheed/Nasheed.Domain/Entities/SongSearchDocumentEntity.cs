using IhsanDev.Shared.Kernel.Entities;

namespace Nasheed.Domain.Entities;

public class SongSearchDocumentEntity : BaseEntity
{
    public int SongId { get; private set; }
    public string SearchText { get; private set; } = string.Empty;

    /// <summary>
    /// JSON-serialized float array representing the pgvector embedding.
    /// Stored as text; parsed in Infrastructure when writing/reading via Npgsql.
    /// </summary>
    public string EmbeddingJson { get; private set; } = string.Empty;

    public string EmbeddingModelKey { get; private set; } = string.Empty;
    public DateTime EmbeddedAt { get; private set; }
    public int IndexVersion { get; private set; }

    // Navigation
    public SongEntity? Song { get; private set; }

    private SongSearchDocumentEntity() { }

    public static SongSearchDocumentEntity Create(
        int songId,
        string searchText,
        string embeddingJson,
        string embeddingModelKey)
    {
        return new SongSearchDocumentEntity
        {
            SongId = songId,
            SearchText = searchText,
            EmbeddingJson = embeddingJson,
            EmbeddingModelKey = embeddingModelKey,
            EmbeddedAt = DateTime.UtcNow,
            IndexVersion = 1
        };
    }

    public void Update(string searchText, string embeddingJson, string embeddingModelKey)
    {
        SearchText = searchText;
        EmbeddingJson = embeddingJson;
        EmbeddingModelKey = embeddingModelKey;
        EmbeddedAt = DateTime.UtcNow;
        IndexVersion++;
    }
}
