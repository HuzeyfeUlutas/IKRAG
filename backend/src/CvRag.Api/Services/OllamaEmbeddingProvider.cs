using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace CvRag.Api.Services;

public class OllamaOptions
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    public string ChatModel { get; set; } = "llama3.1";
}

public class OllamaEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _http;
    private readonly OllamaOptions _options;

    public OllamaEmbeddingProvider(HttpClient http, IOptions<OllamaOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<float[]> EmbedAsync(string text)
    {
        var payload = JsonSerializer.Serialize(new { model = _options.EmbeddingModel, prompt = text });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await _http.PostAsync("/api/embeddings", content);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var embeddingArray = doc.RootElement.GetProperty("embedding");

        var result = new float[embeddingArray.GetArrayLength()];
        for (int i = 0; i < result.Length; i++)
            result[i] = embeddingArray[i].GetSingle();

        return result;
    }
}
