using CvRag.Api.Data;
using CvRag.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CvRag.Api.Controllers;

[ApiController]
[Route("api/job-postings")]
public class JobPostingsController : ControllerBase
{
    private readonly CvRagDbContext _db;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly MatchingService _matchingService;

    public JobPostingsController(CvRagDbContext db, IEmbeddingProvider embeddingProvider, MatchingService matchingService)
    {
        _db = db;
        _embeddingProvider = embeddingProvider;
        _matchingService = matchingService;
    }

    public class CreateRequest
    {
        public string Text { get; set; } = string.Empty;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new { error = "İlan metni boş olamaz." });

        var embedding = await _embeddingProvider.EmbedAsync(request.Text);
        var posting = new Models.JobPosting
        {
            RawText = request.Text,
            EmbeddingVector = new Pgvector.Vector(embedding)
        };
        _db.JobPostings.Add(posting);
        await _db.SaveChangesAsync();

        return Created($"/api/job-postings/{posting.Id}", new { posting.Id, posting.CreatedAt });
    }

    [HttpPost("{id}/match")]
    public async Task<IActionResult> Match(Guid id, [FromQuery] int topN = 10)
    {
        var job = await _db.JobPostings.FindAsync(id);
        if (job is null)
            return NotFound(new { error = "İlan bulunamadı." });

        var results = await _matchingService.ScoreAndRankAsync(job, topN);

        var response = results.Select(r => new
        {
            r.CvDocumentId,
            r.SimilarityScore,
            r.LlmScore,
            r.LlmReasoning
        });

        return Ok(response);
    }
}
