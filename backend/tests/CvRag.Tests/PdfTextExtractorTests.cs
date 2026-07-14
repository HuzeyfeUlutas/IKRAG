using CvRag.Api.Services;
using Xunit;

namespace CvRag.Tests;

public class PdfTextExtractorTests
{
    [Fact]
    public void ExtractText_ReturnsTextContainingKnownPhrase()
    {
        using var stream = File.OpenRead(Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "sample.pdf"));

        var text = PdfTextExtractor.ExtractText(stream);

        Assert.Contains("Ahmet Yilmaz", text);
    }
}
