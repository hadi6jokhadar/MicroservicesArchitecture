using Microsoft.EntityFrameworkCore;
using Nasheed.Domain.Entities;
using Nasheed.Domain.Interfaces;
using Nasheed.Infrastructure.Persistence;

namespace Nasheed.Infrastructure.Persistence.Repositories;

public class SongSearchDocumentRepository : ISongSearchDocumentRepository
{
    private readonly NasheedDbContext _context;

    public SongSearchDocumentRepository(NasheedDbContext context) => _context = context;

    public async Task<SongSearchDocumentEntity?> GetBySongIdAsync(int songId, CancellationToken cancellationToken = default)
    {
        return await _context.SongSearchDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.SongId == songId, cancellationToken);
    }

    public async Task UpsertAsync(SongSearchDocumentEntity document, CancellationToken cancellationToken = default)
    {
        var existing = await _context.SongSearchDocuments
            .FirstOrDefaultAsync(d => d.SongId == document.SongId, cancellationToken);

        if (existing == null)
        {
            await _context.SongSearchDocuments.AddAsync(document, cancellationToken);
        }
        else
        {
            existing.Update(document.SearchText, document.EmbeddingJson, document.EmbeddingModelKey);
            _context.SongSearchDocuments.Update(existing);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<(int SongId, double Score)>> SearchSimilarAsync(
        float[] queryEmbedding,
        int topN,
        CancellationToken cancellationToken = default)
    {
        // Cosine similarity computed in-memory using stored JSON embedding.
        // For production at scale, replace with pgvector extension for server-side vector search.
        var documents = await _context.SongSearchDocuments
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var results = documents
            .Select(d =>
            {
                var storedEmbedding = System.Text.Json.JsonSerializer.Deserialize<float[]>(d.EmbeddingJson);
                if (storedEmbedding == null || storedEmbedding.Length != queryEmbedding.Length)
                    return (d.SongId, Score: 0.0);

                var dotProduct = queryEmbedding.Zip(storedEmbedding, (a, b) => a * b).Sum();
                var normA = Math.Sqrt(queryEmbedding.Sum(x => x * x));
                var normB = Math.Sqrt(storedEmbedding.Sum(x => (double)x * x));

                var score = normA > 0 && normB > 0 ? dotProduct / (normA * normB) : 0.0;
                return (d.SongId, Score: score);
            })
            .OrderByDescending(r => r.Score)
            .Take(topN)
            .ToList();

        return results;
    }
}
