using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace CvRag.Api.Services;

public class OllamaChatProvider : IChatProvider
{
    private readonly HttpClient _http;
    private readonly OllamaOptions _options;

    public OllamaChatProvider(HttpClient http, IOptions<OllamaOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<string> CompleteAsync(
        string systemPrompt, List<(string Role, string Content)> history, string userMessage)
    {
        var messages = new List<object> { new { role = "system", content = systemPrompt } };
        messages.AddRange(history.Select(h => (object)new { role = h.Role, content = h.Content }));
        messages.Add(new { role = "user", content = userMessage });

        var payload = JsonSerializer.Serialize(new
        {
            model = _options.ChatModel,
            messages,
            stream = false
        });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await _http.PostAsync("/api/chat", content);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }
}
