using System.Text.Json;
using CvRag.Api.Data;
using CvRag.Api.Models;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace CvRag.Api.Services;

public class MatchingService
{
    private readonly CvRagDbContext _db;
    private readonly IChatProvider _chatProvider;

    public MatchingService(CvRagDbContext db, IChatProvider chatProvider)
    {
        _db = db;
        _chatProvider = chatProvider;
    }

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

    public async Task<List<MatchResult>> ScoreAndRankAsync(JobPosting job, int topN)
    {
        var candidates = await FindTopCandidatesAsync(job.EmbeddingVector!, topN);
        var results = new List<MatchResult>();

        foreach (var (cv, similarity) in candidates)
        {
            var systemPrompt =
                "Sen bir İK asistanısın. Sana bir iş ilanı ve bir CV metni verilecek. " +
                "CV'nin ilana uygunluğunu 0-100 arasında puanla ve kısa bir gerekçe yaz. " +
                "Yanıtını SADECE şu JSON formatında ver: {\"score\": <int>, \"reasoning\": \"<kısa metin>\"}";
            var userMessage = $"İlan:\n{job.RawText}\n\nCV:\n{cv.RawText}";

            var response = await _chatProvider.CompleteAsync(systemPrompt, new List<(string, string)>(), userMessage);
            using var doc = JsonDocument.Parse(response);
            var score = doc.RootElement.GetProperty("score").GetInt32();
            var reasoning = doc.RootElement.GetProperty("reasoning").GetString() ?? string.Empty;

            var matchResult = new MatchResult
            {
                JobPostingId = job.Id,
                CvDocumentId = cv.Id,
                SimilarityScore = similarity,
                LlmScore = score,
                LlmReasoning = reasoning
            };
            _db.MatchResults.Add(matchResult);
            results.Add(matchResult);
        }

        await _db.SaveChangesAsync();
        return results.OrderByDescending(r => r.LlmScore).ToList();
    }
}
