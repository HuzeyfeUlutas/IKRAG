using System.Text;
using iText.Kernel.Pdf;

namespace CvRag.Api.Services;

public static class PdfTextExtractor
{
    public static string ExtractText(Stream pdfStream)
    {
        using var pdfDocument = new PdfDocument(new PdfReader(pdfStream));
        var sb = new StringBuilder();
        for (int i = 1; i <= pdfDocument.GetNumberOfPages(); i++)
        {
            var page = pdfDocument.GetPage(i);
            sb.AppendLine(iText.Kernel.Pdf.Canvas.Parser.PdfTextExtractor.GetTextFromPage(page));
        }
        return sb.ToString();
    }
}
