using CvRag.Api.Services;
using Xunit;

namespace CvRag.Tests;

public class TextChunkerTests
{
    [Fact]
    public void Split_ShortText_ReturnsSingleChunk()
    {
        var result = TextChunker.Split("kısa bir metin", chunkSize: 500, overlap: 50);
        Assert.Single(result);
        Assert.Equal("kısa bir metin", result[0]);
    }

    [Fact]
    public void Split_LongText_ReturnsMultipleOverlappingChunks()
    {
        var text = new string('a', 1200);
        var result = TextChunker.Split(text, chunkSize: 500, overlap: 50);

        Assert.Equal(3, result.Count);
        Assert.Equal(500, result[0].Length);
        // İkinci chunk, birincinin son 50 karakteriyle örtüşmeli
        Assert.Equal(text.Substring(450, 50), result[1].Substring(0, 50));
    }

    [Fact]
    public void Split_EmptyText_ReturnsEmptyList()
    {
        var result = TextChunker.Split("", chunkSize: 500, overlap: 50);
        Assert.Empty(result);
    }
}
