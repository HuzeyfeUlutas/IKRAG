namespace CvRag.Api.Models;

public class MatchResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JobPostingId { get; set; }
    public Guid CvDocumentId { get; set; }
    public double SimilarityScore { get; set; }
    public int LlmScore { get; set; }
    public string LlmReasoning { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
