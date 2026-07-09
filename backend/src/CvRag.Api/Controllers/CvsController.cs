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

    public CvsController(CvRagDbContext db) => _db = db;

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

        var cv = new Models.CvDocument
        {
            FileName = file.FileName,
            RawText = text
        };
        _db.CvDocuments.Add(cv);
        await _db.SaveChangesAsync();

        return Created($"/api/cvs/{cv.Id}", new { cv.Id, cv.FileName, cv.CreatedAt });
    }
}
