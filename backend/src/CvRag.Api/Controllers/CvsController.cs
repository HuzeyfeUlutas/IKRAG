using CvRag.Api.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CvRag.Api.Controllers;

[ApiController]
[Route("api/cvs")]
public class CvsController : ControllerBase
{
    private readonly CvRagDbContext _db;
    private readonly Services.IEmbeddingProvider _embeddingProvider;
    private readonly Services.CvChatService _chatService;

    public CvsController(CvRagDbContext db, Services.IEmbeddingProvider embeddingProvider, Services.CvChatService chatService)
    {
        _db = db;
        _embeddingProvider = embeddingProvider;
        _chatService = chatService;
    }

    public class ChatRequest
    {
        public string Question { get; set; } = string.Empty;
    }

    [HttpPost("{id}/chat")]
    public async Task<IActionResult> Chat(Guid id, [FromBody] ChatRequest request)
    {
        var exists = await _db.CvDocuments.AnyAsync(c => c.Id == id);
        if (!exists)
            return NotFound(new { error = "CV bulunamadı." });

        var answer = await _chatService.AskAsync(id, request.Question);
        return Ok(new { answer });
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var cvs = await _db.CvDocuments
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new { c.Id, c.FileName, c.CreatedAt })
            .ToListAsync();
        return Ok(cvs);
    }

    [HttpPost]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file.Length == 0)
            return BadRequest(new { error = "Dosya boş." });

        await using var stream = file.OpenReadStream();
        var text = Services.PdfTextExtractor.ExtractText(stream);

        var embedding = await _embeddingProvider.EmbedAsync(text);

        var cv = new Models.CvDocument
        {
            FileName = file.FileName,
            RawText = text,
            EmbeddingVector = new Pgvector.Vector(embedding)
        };
        _db.CvDocuments.Add(cv);

        var chunkTexts = Services.TextChunker.Split(text);
        for (int i = 0; i < chunkTexts.Count; i++)
        {
            var chunkEmbedding = await _embeddingProvider.EmbedAsync(chunkTexts[i]);
            _db.CvChunks.Add(new Models.CvChunk
            {
                CvDocumentId = cv.Id,
                ChunkText = chunkTexts[i],
                ChunkIndex = i,
                EmbeddingVector = new Pgvector.Vector(chunkEmbedding)
            });
        }

        await _db.SaveChangesAsync();

        return Created($"/api/cvs/{cv.Id}", new { cv.Id, cv.FileName, cv.CreatedAt });
    }
}
