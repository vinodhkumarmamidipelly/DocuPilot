using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml.Packaging;
using DocumentMergeApi.Models;

namespace DocumentMergeApi.Services;

public sealed class TokenDetector
{
    private static readonly Regex TokenRegex = new(@"\[(?<token>[A-Za-z0-9_]+)\]", RegexOptions.Compiled);

    public IList<TokenOccurrence> GetTokens(WordprocessingDocument templateDoc)
    {
        var tokens = new List<TokenOccurrence>();
        var body = templateDoc.MainDocumentPart!.Document.Body!;
        var paragraphs = body.Descendants<Paragraph>();
        int index = 0;

        foreach (var para in paragraphs)
        {
            var text = string.Concat(para.Descendants<Text>().Select(t => t.Text));
            if (string.IsNullOrWhiteSpace(text))
            {
                index++;
                continue;
            }

            foreach (Match match in TokenRegex.Matches(text))
            {
                if (!match.Success) continue;
                var token = match.Groups["token"].Value;
                var isInline = !string.Equals(text.Trim(), match.Value, StringComparison.Ordinal);
                tokens.Add(new TokenOccurrence(token, para, index, isInline));
            }

            index++;
        }

        return tokens;
    }
}


