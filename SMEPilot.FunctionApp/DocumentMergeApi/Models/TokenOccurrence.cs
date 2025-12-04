using DocumentFormat.OpenXml.Wordprocessing;

namespace DocumentMergeApi.Models;

public sealed class TokenOccurrence
{
    public string Token { get; }
    public Paragraph PlaceholderParagraph { get; }
    public int ParagraphIndex { get; }
    public bool IsInline { get; }

    public TokenOccurrence(string token, Paragraph placeholderParagraph, int paragraphIndex, bool isInline)
    {
        Token = token;
        PlaceholderParagraph = placeholderParagraph;
        ParagraphIndex = paragraphIndex;
        IsInline = isInline;
    }
}


