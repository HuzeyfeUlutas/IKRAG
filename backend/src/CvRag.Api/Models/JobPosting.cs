using Pgvector;

namespace CvRag.Api.Models;

public class JobPosting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RawText { get; set; } = string.Empty;
    public Vector? EmbeddingVector { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
