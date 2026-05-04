using Nasheed.Domain.Entities;

namespace Nasheed.Domain.Interfaces;

public interface ISongSearchDocumentRepository
{
    Task<SongSearchDocumentEntity?> GetBySongIdAsync(int songId, CancellationToken cancellationToken = default);
    Task UpsertAsync(SongSearchDocumentEntity document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fast lexical pre-search on stored search text to avoid embedding calls when direct text matches exist.
    /// Returns matching song IDs ordered by latest document id.
    /// </summary>
    Task<List<int>> SearchByTextAsync(
        string query,
        int topN = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the top-N most similar songs to the provided embedding using cosine similarity.
    /// Returns (SongId, similarity score) pairs ordered by descending similarity.
    /// </summary>
    Task<List<(int SongId, double Score)>> SearchSimilarAsync(
        float[] queryEmbedding,
        int topN = 10,
        CancellationToken cancellationToken = default);
    Task DeleteBySongIdAsync(int songId, CancellationToken cancellationToken = default);
}
