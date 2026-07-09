using Pgvector;

namespace CvRag.Api.Models;

public class CvChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CvDocumentId { get; set; }
    public string ChunkText { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public Vector? EmbeddingVector { get; set; }
}
