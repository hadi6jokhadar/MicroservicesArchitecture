using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nasheed.Domain.Entities;
using Nasheed.Domain.Interfaces;
using Nasheed.Infrastructure.Persistence;

namespace Nasheed.Infrastructure.Persistence.Repositories;

public class SongSearchDocumentRepository : ISongSearchDocumentRepository
{
    private readonly NasheedDbContext _context;
    private readonly ILogger<SongSearchDocumentRepository> _logger;

    public SongSearchDocumentRepository(
        NasheedDbContext context,
        ILogger<SongSearchDocumentRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

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

    public async Task<List<int>> SearchByTextAsync(
        string query,
        int topN = 10,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = query?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return new List<int>();
        }

        var normalizedTopN = topN > 0 ? topN : 10;
        var pattern = $"%{normalizedQuery.ToLowerInvariant()}%";

        return await _context.SongSearchDocuments
            .AsNoTracking()
            .Where(d => EF.Functions.Like(d.SearchText.ToLower(), pattern))
            .OrderByDescending(d => d.Id)
            .Select(d => d.SongId)
            .Take(normalizedTopN)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<(int SongId, double Score)>> SearchSimilarAsync(
        float[] queryEmbedding,
        int topN,
        CancellationToken cancellationToken = default)
    {
        if (queryEmbedding.Length == 0)
        {
            return new List<(int SongId, double Score)>();
        }

        var normalizedTopN = topN > 0 ? topN : 10;
        var queryVector = $"[{string.Join(",", queryEmbedding.Select(v => v.ToString("G9", CultureInfo.InvariantCulture)))}]";

        try
        {
            return await SearchSimilarWithPgVectorAsync(queryVector, normalizedTopN, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "pgvector similarity search failed. Falling back to in-memory cosine search");
            return await SearchSimilarInMemoryAsync(queryEmbedding, normalizedTopN, cancellationToken);
        }
    }

    private async Task<List<(int SongId, double Score)>> SearchSimilarWithPgVectorAsync(
        string queryVector,
        int topN,
        CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT ""SongId"", (1 - (""EmbeddingJson""::vector <=> CAST(@queryVector AS vector))) AS ""Score""
FROM ""SongSearchDocuments""
WHERE COALESCE(""EmbeddingJson"", '') <> ''
ORDER BY ""EmbeddingJson""::vector <=> CAST(@queryVector AS vector)
LIMIT @topN;";

            var vectorParam = command.CreateParameter();
            vectorParam.ParameterName = "@queryVector";
            vectorParam.Value = queryVector;
            command.Parameters.Add(vectorParam);

            var topNParam = command.CreateParameter();
            topNParam.ParameterName = "@topN";
            topNParam.Value = topN;
            command.Parameters.Add(topNParam);

            var results = new List<(int SongId, double Score)>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var songId = reader.GetInt32(0);
                var score = reader.IsDBNull(1)
                    ? 0d
                    : Convert.ToDouble(reader.GetValue(1), CultureInfo.InvariantCulture);

                results.Add((songId, score));
            }

            return results;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<List<(int SongId, double Score)>> SearchSimilarInMemoryAsync(
        float[] queryEmbedding,
        int topN,
        CancellationToken cancellationToken)
    {
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
