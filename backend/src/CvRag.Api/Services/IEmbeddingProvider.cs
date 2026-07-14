namespace CvRag.Api.Services;

public interface IEmbeddingProvider
{
    Task<float[]> EmbedAsync(string text);
}
