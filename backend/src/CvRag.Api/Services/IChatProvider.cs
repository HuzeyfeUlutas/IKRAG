namespace CvRag.Api.Services;

public interface IChatProvider
{
    Task<string> CompleteAsync(string systemPrompt, List<(string Role, string Content)> history, string userMessage);
}
