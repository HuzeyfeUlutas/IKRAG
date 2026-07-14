namespace CvRag.Api.Models;

public class ChatMessageEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CvDocumentId { get; set; }
    public string Role { get; set; } = string.Empty; // "user" | "assistant"
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
