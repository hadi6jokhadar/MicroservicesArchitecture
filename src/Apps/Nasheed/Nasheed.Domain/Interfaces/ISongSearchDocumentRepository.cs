using Nasheed.Domain.Entities;

namespace Nasheed.Domain.Interfaces;

public interface ISongSearchDocumentRepository
{
    Task<SongSearchDocumentEntity?> GetBySongIdAsync(int songId, CancellationToken cancellationToken = default);
    Task UpsertAsync(SongSearchDocumentEntity document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the top-N most similar songs to the provided embedding using cosine similarity.
    /// Returns (SongId, similarity score) pairs ordered by descending similarity.
    /// </summary>
    Task<List<(int SongId, double Score)>> SearchSimilarAsync(
        float[] queryEmbedding,
        int topN = 10,
        CancellationToken cancellationToken = default);
}
