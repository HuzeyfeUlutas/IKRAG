using CvRag.Api.Controllers;
using CvRag.Api.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace CvRag.Tests;

public class FakeEmbeddingProvider : CvRag.Api.Services.IEmbeddingProvider
{
    public Task<float[]> EmbedAsync(string text) => Task.FromResult(new float[768]);
}

public class CvsControllerTests
{
    private static CvRagDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<CvRagDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CvRagDbContext(options);
    }

    [Fact]
    public async Task Upload_SavesCvDocumentAndReturnsCreated()
    {
        var db = CreateInMemoryDb();
        var chatService = new CvRag.Api.Services.CvChatService(db, new FakeEmbeddingProvider(), new RecordingChatProvider(""));
        var controller = new CvsController(db, new FakeEmbeddingProvider(), chatService);

        var bytes = await File.ReadAllBytesAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample.pdf"));
        var formFile = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "sample.pdf");

        var result = await controller.Upload(formFile);

        var created = Assert.IsType<CreatedResult>(result);
        Assert.Single(db.CvDocuments);
        Assert.Contains("Ahmet Yilmaz", db.CvDocuments.First().RawText);
        Assert.NotNull(db.CvDocuments.First().EmbeddingVector);

        var chunks = db.CvChunks.Where(c => c.CvDocumentId == db.CvDocuments.First().Id).ToList();
        Assert.NotEmpty(chunks);
        Assert.All(chunks, c => Assert.NotNull(c.EmbeddingVector));
    }
}
