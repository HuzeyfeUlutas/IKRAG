using System.Net;
using System.Text;
using System.Text.Json;
using CvRag.Api.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace CvRag.Tests;

public class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly string _responseJson;
    public FakeHttpMessageHandler(string responseJson) => _responseJson = responseJson;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_responseJson, Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}

public class OllamaEmbeddingProviderTests
{
    [Fact]
    public async Task EmbedAsync_ParsesEmbeddingFromOllamaResponse()
    {
        var fakeJson = JsonSerializer.Serialize(new { embedding = new[] { 0.1f, 0.2f, 0.3f } });
        var handler = new FakeHttpMessageHandler(fakeJson);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") };
        var options = Options.Create(new OllamaOptions { EmbeddingModel = "nomic-embed-text" });

        var provider = new OllamaEmbeddingProvider(httpClient, options);
        var result = await provider.EmbedAsync("test metni");

        Assert.Equal(new[] { 0.1f, 0.2f, 0.3f }, result);
    }
}
