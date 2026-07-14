using CvRag.Api.Data;
using CvRag.Api.Models;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace CvRag.Api.Services;

public class CvChatService
{
    private const int TopK = 4;
    private readonly CvRagDbContext _db;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IChatProvider _chatProvider;

    public CvChatService(CvRagDbContext db, IEmbeddingProvider embeddingProvider, IChatProvider chatProvider)
    {
        _db = db;
        _embeddingProvider = embeddingProvider;
        _chatProvider = chatProvider;
    }

    public async Task<string> AskAsync(Guid cvId, string question)
    {
        var questionEmbedding = await _embeddingProvider.EmbedAsync(question);
        var queryVector = new Pgvector.Vector(questionEmbedding);

        var relevantChunks = await _db.CvChunks
            .Where(c => c.CvDocumentId == cvId && c.EmbeddingVector != null)
            .OrderBy(c => c.EmbeddingVector!.CosineDistance(queryVector))
            .Take(TopK)
            .Select(c => c.ChunkText)
            .ToListAsync();

        var history = await _db.ChatMessages
            .Where(m => m.CvDocumentId == cvId)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new ValueTuple<string, string>(m.Role, m.Content))
            .ToListAsync();

        var systemPrompt =
            "Sen bir İK asistanısın. Sana bir adayın CV'sinden alınmış ilgili parçalar verilecek. " +
            "Sadece bu parçalara dayanarak soruyu cevapla. Parçalarda cevap yoksa bunu belirt.";
        var context = string.Join("\n---\n", relevantChunks);
        var userMessage = $"CV parçaları:\n{context}\n\nSoru: {question}";

        var answer = await _chatProvider.CompleteAsync(systemPrompt, history, userMessage);

        _db.ChatMessages.Add(new ChatMessageEntity { CvDocumentId = cvId, Role = "user", Content = question });
        _db.ChatMessages.Add(new ChatMessageEntity { CvDocumentId = cvId, Role = "assistant", Content = answer });
        await _db.SaveChangesAsync();

        return answer;
    }
}
