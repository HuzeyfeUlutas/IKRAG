using CvRag.Api.Data;
using CvRag.Api.Models;
using CvRag.Api.Services;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Xunit;

namespace CvRag.Tests;

public class MatchingServiceTests : IDisposable
{
    private readonly CvRagDbContext _db;
    private CvDocument? _closeCv;
    private CvDocument? _farCv;
    private CvDocument? _scoreCv;
    private JobPosting? _scoreJob;
    private List<MatchResult> _createdMatchResults = new();

    public MatchingServiceTests()
    {
        var options = new DbContextOptionsBuilder<CvRagDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=cvrag;Username=cvrag;Password=cvrag",
                o => o.UseVector())
            .Options;
        _db = new CvRagDbContext(options);
        _db.Database.EnsureCreated();
    }

    [Fact]
    public async Task FindTopCandidatesAsync_ReturnsClosestVectorFirst()
    {
        // Cosine distance measures angle, not magnitude, so the CV vectors must differ
        // in DIRECTION from the job vector (all 1.0f) to be distinguishable.
        //   close: first 700 dims = 1.0f, last 68 dims = 0.5f
        //     dot(job, close) = 700*1 + 68*0.5 = 734
        //     |close| = sqrt(700 + 68*0.25) = sqrt(717), |job| = sqrt(768)
        //     cos_sim = 734 / sqrt(717*768) ~= 0.9891  ->  distance ~= 0.011
        //   far: first 384 dims = 1.0f, last 384 dims = -1.0f (orthogonal to job)
        //     dot(job, far) = 384*1 + 384*(-1) = 0  ->  cos_sim = 0 -> distance = 1.0
        // close distance (~0.011) << far distance (~1.0): no near-tie, no fp flip risk.
        _closeCv = new CvDocument { FileName = "close.pdf", RawText = "x", EmbeddingVector = new Vector(MakeCloseVector()) };
        _farCv = new CvDocument { FileName = "far.pdf", RawText = "x", EmbeddingVector = new Vector(MakeFarVector()) };
        _db.CvDocuments.AddRange(_closeCv, _farCv);
        await _db.SaveChangesAsync();

        var fakeChat = new FakeChatProvider("{\"score\": 0, \"reasoning\": \"\"}");
        var service = new MatchingService(_db, fakeChat);
        var jobEmbedding = new Vector(MakeVector(1.0f));

        var results = await service.FindTopCandidatesAsync(jobEmbedding, topN: 2);

        Assert.Equal("close.pdf", results[0].Cv.FileName);
    }

    [Fact]
    public async Task ScoreAndRankAsync_UsesLlmToScoreTopCandidates()
    {
        _scoreCv = new CvDocument { FileName = "aday.pdf", RawText = "5 yil .NET deneyimi", EmbeddingVector = new Vector(MakeVector(0.9f)) };
        _db.CvDocuments.Add(_scoreCv);
        _scoreJob = new JobPosting { RawText = ".NET Backend Developer araniyoo", EmbeddingVector = new Vector(MakeVector(1.0f)) };
        _db.JobPostings.Add(_scoreJob);
        await _db.SaveChangesAsync();

        var fakeChat = new FakeChatProvider("{\"score\": 85, \"reasoning\": \"Gucli .NET deneyimi var.\"}");
        var service = new MatchingService(_db, fakeChat);

        var results = await service.ScoreAndRankAsync(_scoreJob, topN: 1);
        _createdMatchResults = results;

        Assert.Single(results);
        Assert.Equal(85, results[0].LlmScore);
        Assert.Equal("Gucli .NET deneyimi var.", results[0].LlmReasoning);
    }

    public class FakeChatProvider : IChatProvider
    {
        private readonly string _response;
        public FakeChatProvider(string response) => _response = response;
        public Task<string> CompleteAsync(string systemPrompt, List<(string Role, string Content)> history, string userMessage)
            => Task.FromResult(_response);
    }

    private static float[] MakeVector(float value)
    {
        var arr = new float[768];
        Array.Fill(arr, value);
        return arr;
    }

    // Points in nearly the same direction as the job vector (all 1.0f): small cosine distance.
    private static float[] MakeCloseVector()
    {
        var arr = new float[768];
        Array.Fill(arr, 1.0f);
        for (var i = 700; i < 768; i++)
        {
            arr[i] = 0.5f;
        }
        return arr;
    }

    // Orthogonal to the job vector: large cosine distance (~1.0).
    private static float[] MakeFarVector()
    {
        var arr = new float[768];
        for (var i = 0; i < 768; i++)
        {
            arr[i] = i < 384 ? 1.0f : -1.0f;
        }
        return arr;
    }

    public void Dispose()
    {
        // Only remove the rows this test created — never the whole shared table.
        var matchIds = _createdMatchResults.Select(m => m.Id).ToArray();
        if (matchIds.Length > 0)
        {
            var matchesToRemove = _db.MatchResults.Where(m => matchIds.Contains(m.Id));
            _db.MatchResults.RemoveRange(matchesToRemove);
        }

        var cvIds = new[] { _closeCv?.Id ?? Guid.Empty, _farCv?.Id ?? Guid.Empty, _scoreCv?.Id ?? Guid.Empty }
            .Where(id => id != Guid.Empty)
            .ToArray();
        if (cvIds.Length > 0)
        {
            var cvsToRemove = _db.CvDocuments.Where(c => cvIds.Contains(c.Id));
            _db.CvDocuments.RemoveRange(cvsToRemove);
        }

        var jobIds = new[] { _scoreJob?.Id ?? Guid.Empty }
            .Where(id => id != Guid.Empty)
            .ToArray();
        if (jobIds.Length > 0)
        {
            var jobsToRemove = _db.JobPostings.Where(j => jobIds.Contains(j.Id));
            _db.JobPostings.RemoveRange(jobsToRemove);
        }

        _db.SaveChanges();
        _db.Dispose();
    }
}
