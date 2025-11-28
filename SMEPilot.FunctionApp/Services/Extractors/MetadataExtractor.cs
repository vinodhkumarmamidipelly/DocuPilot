using System;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SMEPilot.FunctionApp.Models;

namespace SMEPilot.FunctionApp.Services.Extractors
{
    /// <summary>
    /// Extracts metadata from document header (first 2000 chars)
    /// Uses strict regex patterns - returns null if not found
    /// </summary>
    public class MetadataExtractor
    {
        private readonly ILogger<MetadataExtractor>? _logger;

        public MetadataExtractor(ILogger<MetadataExtractor>? logger = null)
        {
            _logger = logger;
        }

        public MetadataData Extract(string fullText)
        {
            var header = fullText.Length > 2000 
                ? fullText.Substring(0, 2000) 
                : fullText;

            return new MetadataData
            {
                ProjectName = ExtractProjectName(header),
                Version = ExtractVersion(header),
                Date = ExtractDate(header),
                Status = ExtractStatus(header),
                Author = ExtractAuthor(header),
                Reviewer = ExtractReviewer(header),
                Approver = ExtractApprover(header)
            };
        }

        private string? ExtractProjectName(string text)
        {
            // Try multiple patterns in order of specificity
            
            // Pattern 1: **Project Name:** or **Project:** followed by value
            var pattern1 = @"(?:\*\*)?(?:Project\s*Name|Project)\s*:?\s*\*?\*?\s*([A-Z][A-Za-z0-9\s\-&]+?)(?:\s*\\|\s*$|\s*\n|\s+\d+\.|\s+##)";
            var match = Regex.Match(text, pattern1, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (match.Success)
            {
                var name = match.Groups[1].Value.Trim().Trim('*', '\\', ')', ']', '[', '(', '|');
                if (name.Length >= 5 && name.Length < 200)
                {
                    _logger?.LogDebug("✅ [METADATA] Extracted Project Name: {Name}", name);
                    return name;
                }
            }

            // Pattern 2: "Project Name: X" (without markdown)
            var pattern2 = @"Project\s*Name\s*:\s*([A-Z][A-Za-z0-9\s\-&]+?)(?:\s*$|\s*\n|\s+Version)";
            match = Regex.Match(text, pattern2, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (match.Success)
            {
                var name = match.Groups[1].Value.Trim();
                if (name.Length >= 5 && name.Length < 200)
                {
                    _logger?.LogDebug("✅ [METADATA] Extracted Project Name (pattern 2): {Name}", name);
                    return name;
                }
            }

            // Pattern 3: Markdown header ## Project Name (but not "FUNCTIONAL SPECIFICATION")
            var pattern3 = @"^##\s+([A-Z][A-Za-z0-9\s\-&]+?)(?:\s*$|\s*\n)";
            match = Regex.Match(text, pattern3, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (match.Success)
            {
                var name = match.Groups[1].Value.Trim();
                if (name.Length >= 5 && name.Length < 200 && 
                    !name.Contains("SPECIFICATION", StringComparison.OrdinalIgnoreCase) &&
                    !name.Contains("DOCUMENT", StringComparison.OrdinalIgnoreCase))
                {
                    _logger?.LogDebug("✅ [METADATA] Extracted Project Name from header: {Name}", name);
                    return name;
                }
            }

            // Pattern 4: First heading after document title that looks like project name
            var pattern4 = @"^##\s+[A-Z][A-Za-z\s]+?\s*-\s*([A-Z][A-Za-z0-9\s\-&]+?)(?:\s*$|\s*\n)";
            match = Regex.Match(text, pattern4, RegexOptions.Multiline);
            if (match.Success)
            {
                var name = match.Groups[1].Value.Trim();
                if (name.Length >= 5 && name.Length < 200)
                {
                    _logger?.LogDebug("✅ [METADATA] Extracted Project Name from subtitle: {Name}", name);
                    return name;
                }
            }

            // Pattern 5: Look for "Project:" in first paragraph
            var firstPara = text.Substring(0, Math.Min(500, text.Length));
            var pattern5 = @"Project\s*:\s*([A-Z][A-Za-z0-9\s\-&]+?)(?:\s*$|\s*\n|\s+Version)";
            match = Regex.Match(firstPara, pattern5, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var name = match.Groups[1].Value.Trim();
                if (name.Length >= 5 && name.Length < 200)
                {
                    _logger?.LogDebug("✅ [METADATA] Extracted Project Name from first para: {Name}", name);
                    return name;
                }
            }

            _logger?.LogDebug("⚠️ [METADATA] Project Name not found (tried 5 patterns)");
            return null;
        }

        private string? ExtractVersion(string text)
        {
            // Pattern: **Version:** 1.0 or Version: 1.0
            var patterns = new[]
            {
                @"(?:\*\*)?Version\s*:?\s*\*?\*?\s*([0-9]+(?:\.[0-9]+)*)",
                @"Version\s*:?\s*([0-9]+(?:\.[0-9]+)*)"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var version = match.Groups[1].Value.Trim();
                    _logger?.LogDebug("✅ [METADATA] Extracted Version: {Version}", version);
                    return version;
                }
            }

            _logger?.LogDebug("⚠️ [METADATA] Version not found");
            return null;
        }

        private string? ExtractDate(string text)
        {
            // Priority 1: "Month Year" format (e.g., "December 2024")
            var pattern1 = @"(?:\*\*)?Date\s*:?\s*\*?\*?\s*([A-Z][a-z]+\s+\d{4})";
            var match = Regex.Match(text, pattern1, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var date = match.Groups[1].Value.Trim();
                _logger?.LogDebug("✅ [METADATA] Extracted Date: {Date}", date);
                return date;
            }

            // Priority 2: ISO date (YYYY-MM-DD)
            var pattern2 = @"(?:\*\*)?Date\s*:?\s*\*?\*?\s*(\d{4}-\d{2}-\d{2})";
            match = Regex.Match(text, pattern2, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var date = match.Groups[1].Value.Trim();
                _logger?.LogDebug("✅ [METADATA] Extracted Date: {Date}", date);
                return date;
            }

            _logger?.LogDebug("⚠️ [METADATA] Date not found");
            return null;
        }

        private string? ExtractStatus(string text)
        {
            // Pattern: **Status:** Draft for Review or Status: DRAFT
            var patterns = new[]
            {
                @"(?:\*\*)?Status\s*:?\s*\*?\*?\s*([A-Z][A-Za-z\s]+?)(?:\s*\\|\s*$|\s*\n|\s+\d+\.|\s+##)",
                @"Status\s*:?\s*([A-Z][A-Za-z\s]+?)(?:\s*\\|\s*$|\s*\n)"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                if (match.Success)
                {
                    var status = match.Groups[1].Value.Trim().Trim('*', '\\');
                    if (status.Length >= 3 && status.Length < 100)
                    {
                        _logger?.LogDebug("✅ [METADATA] Extracted Status: {Status}", status);
                        return status;
                    }
                }
            }

            _logger?.LogDebug("⚠️ [METADATA] Status not found");
            return null;
        }

        private string? ExtractAuthor(string text)
        {
            // Pattern: **Author(s):** Name or Author: Name
            var patterns = new[]
            {
                @"(?:\*\*)?Author(?:\(s\))?\s*:?\s*\*?\*?\s*([A-Z][A-Za-z\s\.\-]{2,50}?)(?=\s*$|\s*\\|\s*\n|\s+\*\*|Reviewer|Approver)",
                @"Author(?:\(s\))?\s*:?\s*([A-Z][A-Za-z\s\.\-]{2,50}?)(?=\s*$|\s*\\|\s*\n|Reviewer|Approver)"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                if (match.Success)
                {
                    var author = match.Groups[1].Value.Trim().Trim('*', '\\');
                    if (IsValidName(author))
                    {
                        _logger?.LogDebug("✅ [METADATA] Extracted Author: {Author}", author);
                        return author;
                    }
                }
            }

            _logger?.LogDebug("⚠️ [METADATA] Author not found");
            return null;
        }

        private string? ExtractReviewer(string text)
        {
            // Pattern: **Reviewer(s):** Name
            var patterns = new[]
            {
                @"(?:\*\*)?Reviewer(?:\(s\))?\s*:?\s*\*?\*?\s*([A-Z][A-Za-z\s\.\-]{2,50}?)(?=\s*$|\s*\\|\s*\n|\s+\*\*|Author|Approver)",
                @"Reviewer(?:\(s\))?\s*:?\s*([A-Z][A-Za-z\s\.\-]{2,50}?)(?=\s*$|\s*\\|\s*\n|Author|Approver)"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                if (match.Success)
                {
                    var reviewer = match.Groups[1].Value.Trim().Trim('*', '\\');
                    if (IsValidName(reviewer))
                    {
                        _logger?.LogDebug("✅ [METADATA] Extracted Reviewer: {Reviewer}", reviewer);
                        return reviewer;
                    }
                }
            }

            _logger?.LogDebug("⚠️ [METADATA] Reviewer not found");
            return null;
        }

        private string? ExtractApprover(string text)
        {
            // Pattern: **Approver(s):** Name
            var patterns = new[]
            {
                @"(?:\*\*)?Approver(?:\(s\))?\s*:?\s*\*?\*?\s*([A-Z][A-Za-z\s\.\-]{2,50}?)(?=\s*$|\s*\\|\s*\n|\s+\*\*|Author|Reviewer)",
                @"Approver(?:\(s\))?\s*:?\s*([A-Z][A-Za-z\s\.\-]{2,50}?)(?=\s*$|\s*\\|\s*\n|Author|Reviewer)"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                if (match.Success)
                {
                    var approver = match.Groups[1].Value.Trim().Trim('*', '\\');
                    if (IsValidName(approver))
                    {
                        _logger?.LogDebug("✅ [METADATA] Extracted Approver: {Approver}", approver);
                        return approver;
                    }
                }
            }

            _logger?.LogDebug("⚠️ [METADATA] Approver not found");
            return null;
        }

        private bool IsValidName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            name = name.Trim();
            
            // Must start with capital letter
            if (!char.IsUpper(name[0]))
                return false;

            // Must be reasonable length
            if (name.Length < 2 || name.Length > 100)
                return false;

            // Must not be document title
            if (name.Contains("FUNCTIONAL SPECIFICATION", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("TECHNICAL SPECIFICATION", StringComparison.OrdinalIgnoreCase))
                return false;

            // Must not be just "Content" or placeholder text
            if (name.Equals("Content", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }
    }
}

