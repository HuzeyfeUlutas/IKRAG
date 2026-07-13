using CvRag.Api.Data;
using CvRag.Api.Models;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace CvRag.Api.Services;

public class MatchingService
{
    private readonly CvRagDbContext _db;

    public MatchingService(CvRagDbContext db) => _db = db;

    public async Task<List<(CvDocument Cv, double Similarity)>> FindTopCandidatesAsync(Vector jobEmbedding, int topN)
    {
        var results = await _db.CvDocuments
            .Where(c => c.EmbeddingVector != null)
            .OrderBy(c => c.EmbeddingVector!.CosineDistance(jobEmbedding))
            .Take(topN)
            .Select(c => new { Cv = c, Distance = c.EmbeddingVector!.CosineDistance(jobEmbedding) })
            .ToListAsync();

        return results.Select(r => (r.Cv, 1.0 - r.Distance)).ToList();
    }
}
