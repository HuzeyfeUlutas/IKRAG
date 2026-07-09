namespace CvRag.Api.Services;

public static class TextChunker
{
    public static List<string> Split(string text, int chunkSize = 500, int overlap = 50)
    {
        var chunks = new List<string>();
        if (string.IsNullOrEmpty(text))
            return chunks;

        if (text.Length <= chunkSize)
        {
            chunks.Add(text);
            return chunks;
        }

        int start = 0;
        int step = chunkSize - overlap;
        while (start < text.Length)
        {
            int length = Math.Min(chunkSize, text.Length - start);
            chunks.Add(text.Substring(start, length));
            if (start + length >= text.Length)
                break;
            start += step;
        }

        return chunks;
    }
}
