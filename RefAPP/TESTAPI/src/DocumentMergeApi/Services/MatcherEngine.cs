using DocumentMergeApi.Models;

namespace DocumentMergeApi.Services;

public sealed class MatcherEngine
{
    public Dictionary<TokenOccurrence, RawSection?> Match(IList<TokenOccurrence> tokens, IList<RawSection> sections)
    {
        var result = new Dictionary<TokenOccurrence, RawSection?>();
        foreach (var token in tokens)
        {
            var matched = MatchToken(token.Token, sections);
            result[token] = matched;
        }
        return result;
    }

    private RawSection? MatchToken(string token, IList<RawSection> sections)
    {
        // 1. Numeric Match
        if (int.TryParse(token, out var num))
        {
            var s = sections.FirstOrDefault(x => x.Key == num);
            if (s != null) return s;
        }

        // 2. Exact Heading Match (Normalized)
        var normToken = Normalize(token);
        var exact = sections.FirstOrDefault(s => Normalize(s.HeadingText) == normToken);
        if (exact != null) return exact;

        // 3. Substring Match
        var sub = sections.FirstOrDefault(s => Normalize(s.HeadingText).Contains(normToken));
        if (sub != null) return sub;

        // 4. Keyword Match (Scoring)
        var tokenWords = normToken.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var best = sections
            .Select(s => new { s, score = Score(tokenWords, Normalize(s.HeadingText)) })
            .OrderByDescending(x => x.score)
            .FirstOrDefault();

        return best is { score: >= 1 } ? best.s : null;
    }

    private static string Normalize(string s) =>
        string.Join(' ', s.ToLowerInvariant().Replace("_", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static int Score(HashSet<string> tokenWords, string headingNorm)
    {
        var hWords = headingNorm.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return hWords.Count(tokenWords.Contains);
    }
}

