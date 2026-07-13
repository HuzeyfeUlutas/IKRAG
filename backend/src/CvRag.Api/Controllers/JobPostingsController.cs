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

    public JobPostingsController(CvRagDbContext db, IEmbeddingProvider embeddingProvider)
    {
        _db = db;
        _embeddingProvider = embeddingProvider;
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
}
