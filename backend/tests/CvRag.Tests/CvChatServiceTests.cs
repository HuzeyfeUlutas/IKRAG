using CvRag.Api.Data;
using CvRag.Api.Models;
using CvRag.Api.Services;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Xunit;

namespace CvRag.Tests;

public class CvChatServiceTests : IDisposable
{
    private readonly CvRagDbContext _db;
    private CvDocument? _cv;

    public CvChatServiceTests()
    {
        var options = new DbContextOptionsBuilder<CvRagDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=cvrag;Username=cvrag;Password=cvrag",
                o => o.UseVector())
            .Options;
        _db = new CvRagDbContext(options);
        _db.Database.EnsureCreated();
    }

    [Fact]
    public async Task AskAsync_RetrievesRelevantChunksAndReturnsAnswer()
    {
        var cv = new CvDocument { FileName = "aday.pdf", RawText = "..." };
        _cv = cv;
        _db.CvDocuments.Add(cv);
        _db.CvChunks.Add(new CvChunk
        {
            CvDocumentId = cv.Id,
            ChunkText = "5 yıl .NET backend deneyimi",
            ChunkIndex = 0,
            EmbeddingVector = new Vector(MakeVector(0.9f))
        });
        await _db.SaveChangesAsync();

        var fakeEmbedding = new FakeEmbeddingProviderReturning(MakeVector(1.0f));
        var fakeChat = new RecordingChatProvider("Adayın 5 yıl .NET deneyimi var.");
        var service = new CvChatService(_db, fakeEmbedding, fakeChat);

        var answer = await service.AskAsync(cv.Id, "Kaç yıl deneyimi var?");

        Assert.Equal("Adayın 5 yıl .NET deneyimi var.", answer);
        Assert.Contains("5 yıl .NET backend deneyimi", fakeChat.LastUserMessage);
        Assert.Equal(2, _db.ChatMessages.Count(m => m.CvDocumentId == cv.Id));
    }

    private static float[] MakeVector(float value)
    {
        var arr = new float[768];
        Array.Fill(arr, value);
        return arr;
    }

    public void Dispose()
    {
        // Only remove the rows this test created — never the whole shared table.
        // The cvrag database is shared with the running app and other test fixtures;
        // wiping ChatMessages/CvChunks/CvDocuments entirely would delete unrelated data.
        var cvId = _cv?.Id;
        if (cvId is Guid id)
        {
            _db.ChatMessages.RemoveRange(_db.ChatMessages.Where(m => m.CvDocumentId == id));
            _db.CvChunks.RemoveRange(_db.CvChunks.Where(c => c.CvDocumentId == id));
            _db.CvDocuments.RemoveRange(_db.CvDocuments.Where(c => c.Id == id));
            _db.SaveChanges();
        }

        _db.Dispose();
    }
}

public class FakeEmbeddingProviderReturning : IEmbeddingProvider
{
    private readonly float[] _vector;
    public FakeEmbeddingProviderReturning(float[] vector) => _vector = vector;
    public Task<float[]> EmbedAsync(string text) => Task.FromResult(_vector);
}

public class RecordingChatProvider : IChatProvider
{
    private readonly string _response;
    public string LastUserMessage { get; private set; } = string.Empty;
    public RecordingChatProvider(string response) => _response = response;
    public Task<string> CompleteAsync(string systemPrompt, List<(string Role, string Content)> history, string userMessage)
    {
        LastUserMessage = userMessage;
        return Task.FromResult(_response);
    }
}
