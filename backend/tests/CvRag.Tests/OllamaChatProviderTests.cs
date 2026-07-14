using System.Text.Json;
using CvRag.Api.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace CvRag.Tests;

public class OllamaChatProviderTests
{
    [Fact]
    public async Task CompleteAsync_ParsesAssistantContentFromOllamaResponse()
    {
        var fakeJson = JsonSerializer.Serialize(new
        {
            message = new { role = "assistant", content = "Adayın 3 yıl deneyimi var." }
        });
        var handler = new FakeHttpMessageHandler(fakeJson);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") };
        var options = Options.Create(new OllamaOptions { ChatModel = "llama3.1" });

        var provider = new OllamaChatProvider(httpClient, options);
        var result = await provider.CompleteAsync(
            "Sen bir CV asistanısın.",
            new List<(string, string)>(),
            "Adayın kaç yıl deneyimi var?");

        Assert.Equal("Adayın 3 yıl deneyimi var.", result);
    }
}
