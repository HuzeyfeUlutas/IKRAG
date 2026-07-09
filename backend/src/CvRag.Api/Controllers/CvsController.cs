using CvRag.Api.Data;
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
}
