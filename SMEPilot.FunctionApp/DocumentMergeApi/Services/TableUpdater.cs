using System;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocumentMergeApi.Services;

public sealed class TableUpdater
{
    private static readonly Regex PlaceholderRegex = new(@"\[[^\]]+\]", RegexOptions.Compiled);

    public void AppendVersionHistory(WordprocessingDocument templateDoc, string? author, string? version)
    {
        var table = FindTableByHeader(templateDoc, new[] { "Version", "Date", "Author", "Changes", "Approved By" });
        if (table == null) return;

        // Capture an existing data row (typically the placeholder row) so we can
        // clone its styling (borders, fonts, alignment) for the new row.
        var existingRows = table.Elements<TableRow>().ToList();
        var headerRow = existingRows.FirstOrDefault();
        var templateRow = existingRows.Skip(1).FirstOrDefault();

        RemovePlaceholderRows(table);

        // After removing placeholder rows, if we still have only the header row,
        // this enrichment represents the initial version of the document.
        var rowsAfterCleanup = table.Elements<TableRow>().ToList();
        var isInitialVersion = rowsAfterCleanup.Count <= 1;
        var changesText = isInitialVersion
            ? "Initial Version"
            : "Updated content based on latest raw document";

        TableRow newRow;
        var effectiveVersion = string.IsNullOrWhiteSpace(version) ? "1.0" : version;
        if (templateRow != null)
        {
            // Clone the existing row to preserve all cell/table styling
            newRow = (TableRow)templateRow.CloneNode(true);
            var cells = newRow.Elements<TableCell>().ToList();

            if (cells.Count >= 1) SetCellText(cells[0], effectiveVersion);
            if (cells.Count >= 2) SetCellText(cells[1], DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"));
            if (cells.Count >= 3) SetCellText(cells[2], string.IsNullOrWhiteSpace(author) ? "System Generated" : author!);
            if (cells.Count >= 4) SetCellText(cells[3], changesText);
            if (cells.Count >= 5) SetCellText(cells[4], string.Empty); // Approved By left blank per requirements
        }
        else
        {
            // Fallback: build a simple row if we have no template row to clone
            newRow = new TableRow();
            newRow.Append(Cells(effectiveVersion,
                                DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"),
                                string.IsNullOrWhiteSpace(author) ? "System Generated" : author!,
                                changesText,
                                string.Empty));
        }

        table.Append(newRow);
    }

    /// <summary>
    /// Update the top metadata/header table (Project Name, Version, Date, Author, etc.)
    /// directly from the metadata dictionary and resolved author. This ensures that
    /// single cells don't end up concatenating multiple inline tokens.
    /// </summary>
    public void UpdateHeaderMetadataTable(
        WordprocessingDocument doc,
        IDictionary<string, string> metadata,
        string? author)
    {
        var body = doc.MainDocumentPart!.Document.Body!;

        foreach (var table in body.Elements<Table>())
        {
            var rows = table.Elements<TableRow>().ToList();
            if (rows.Count < 3) continue;

            string GetLeftText(int rowIndex)
            {
                var cell = rows[rowIndex].Elements<TableCell>().FirstOrDefault();
                if (cell == null) return string.Empty;
                var text = string.Concat(cell.Descendants<Text>().Select(t => t.Text ?? string.Empty));
                return (text ?? string.Empty).Trim();
            }

            var r0 = GetLeftText(0);
            var r1 = GetLeftText(1);
            var r2 = GetLeftText(2);

            // Heuristic: match the typical header table used in the SMEPilot template
            if (!r0.Contains("Project Name", StringComparison.OrdinalIgnoreCase) ||
                !r1.Contains("Version", StringComparison.OrdinalIgnoreCase) ||
                !r2.Contains("Date", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            TableCell? GetRightCell(int rowIndex)
            {
                var cells = rows[rowIndex].Elements<TableCell>().ToList();
                return cells.Count >= 2 ? cells[1] : null;
            }

            if (GetRightCell(0) is { } projectCell &&
                metadata.TryGetValue("PROJECT_NAME", out var projectName) &&
                !string.IsNullOrWhiteSpace(projectName))
            {
                ReplaceCellWithSingleParagraph(projectCell, projectName);
            }

            if (GetRightCell(1) is { } versionCell &&
                metadata.TryGetValue("VERSION_NUMBER", out var version) &&
                !string.IsNullOrWhiteSpace(version))
            {
                ReplaceCellWithSingleParagraph(versionCell, version);
            }

            if (GetRightCell(2) is { } dateCell &&
                metadata.TryGetValue("DATE", out var date) &&
                !string.IsNullOrWhiteSpace(date))
            {
                ReplaceCellWithSingleParagraph(dateCell, date);
            }

            if (GetRightCell(3) is { } authorCell)
            {
                // Prefer resolved author (SharePoint uploader) over raw metadata author
                var resolvedAuthor = author;
                if (string.IsNullOrWhiteSpace(resolvedAuthor) &&
                    metadata.TryGetValue("AUTHOR_NAME", out var metaAuthor))
                {
                    resolvedAuthor = metaAuthor;
                }

                if (!string.IsNullOrWhiteSpace(resolvedAuthor))
                {
                    ReplaceCellWithSingleParagraph(authorCell, resolvedAuthor);
                }
            }

            if (GetRightCell(4) is { } reviewerCell &&
                metadata.TryGetValue("REVIEWER_NAME", out var reviewer) &&
                !string.IsNullOrWhiteSpace(reviewer))
            {
                ReplaceCellWithSingleParagraph(reviewerCell, reviewer);
            }

            if (GetRightCell(5) is { } approverCell &&
                metadata.TryGetValue("APPROVER_NAME", out var approver) &&
                !string.IsNullOrWhiteSpace(approver))
            {
                ReplaceCellWithSingleParagraph(approverCell, approver);
            }

            if (rows.Count > 6 && GetRightCell(6) is { } statusCell &&
                metadata.TryGetValue("STATUS", out var status) &&
                !string.IsNullOrWhiteSpace(status))
            {
                ReplaceCellWithSingleParagraph(statusCell, status);
            }

            // Only update the first matching header table
            break;
        }
    }

    public void AppendChangeLog(WordprocessingDocument templateDoc, string? author, string sectionList)
    {
        var table = FindTableByHeader(templateDoc, new[] { "Change #", "Date", "Section", "Description", "Author" });
        if (table == null) return;

        var existingRows = table.Elements<TableRow>().ToList();
        var headerRow = existingRows.FirstOrDefault();
        var templateRow = existingRows.Skip(1).FirstOrDefault();

        RemovePlaceholderRows(table);

        // Determine next Change # by looking at existing data rows (if any)
        var dataRows = table.Elements<TableRow>().Skip(1).ToList();
        var nextChangeNumber = 1;
        foreach (var row in dataRows)
        {
            var firstCell = row.Elements<TableCell>().FirstOrDefault();
            if (firstCell == null) continue;
            var text = string.Concat(firstCell.Descendants<Text>().Select(t => t.Text ?? string.Empty)).Trim();
            if (int.TryParse(text, out var n) && n >= nextChangeNumber)
            {
                nextChangeNumber = n + 1;
            }
        }

        var changeNumberText = nextChangeNumber.ToString();
        var sectionText = string.IsNullOrWhiteSpace(sectionList) ? "Content" : sectionList;
        var descriptionText = $"Updated sections: {sectionText}";

        TableRow newRow;
        if (templateRow != null)
        {
            newRow = (TableRow)templateRow.CloneNode(true);
            var cells = newRow.Elements<TableCell>().ToList();

            if (cells.Count >= 1) SetCellText(cells[0], changeNumberText);
            if (cells.Count >= 2) SetCellText(cells[1], DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"));
            if (cells.Count >= 3) SetCellText(cells[2], sectionText);
            if (cells.Count >= 4) SetCellText(cells[3], descriptionText);
            if (cells.Count >= 5) SetCellText(cells[4], string.IsNullOrWhiteSpace(author) ? "System Generated" : author!);
        }
        else
        {
            newRow = new TableRow();
            newRow.Append(Cells(changeNumberText,
                                DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"),
                                sectionText,
                                descriptionText,
                                string.IsNullOrWhiteSpace(author) ? "System Generated" : author!));
        }

        table.Append(newRow);
    }

    private static TableCell[] Cells(params string[] texts) =>
        texts.Select(t => new TableCell(new Paragraph(new Run(new Text(t))))).ToArray();

    private static Table? FindTableByHeader(WordprocessingDocument doc, string[] headers)
    {
        var tables = doc.MainDocumentPart!.Document.Body!.Elements<Table>();
        foreach (var table in tables)
        {
            var firstRow = table.Elements<TableRow>().FirstOrDefault();
            if (firstRow == null) continue;

            var cells = firstRow.Elements<TableCell>()
                                .Select(c => string.Concat(c.Descendants<Text>().Select(t => t.Text)).Trim())
                                .ToArray();

            if (cells.Length >= headers.Length &&
                headers.SequenceEqual(cells.Take(headers.Length), StringComparer.OrdinalIgnoreCase))
                return table;
        }
        return null;
    }

    private static void RemovePlaceholderRows(Table table)
    {
        var rowsToRemove = table.Elements<TableRow>()
            .Where(row => row.Descendants<Text>().Any(t => PlaceholderRegex.IsMatch(t.Text ?? string.Empty)))
            .ToList();

        foreach (var row in rowsToRemove)
        {
            row.Remove();
        }
    }

    /// <summary>
    /// Replace the textual content of a table cell while preserving its styling.
    /// Only the first Text node is updated; any additional Text nodes are cleared.
    /// </summary>
    private static void SetCellText(TableCell cell, string text)
    {
        var textNodes = cell.Descendants<Text>().ToList();
        if (textNodes.Count == 0)
        {
            var paragraph = cell.GetFirstChild<Paragraph>() ?? cell.AppendChild(new Paragraph());
            var run = paragraph.GetFirstChild<Run>() ?? paragraph.AppendChild(new Run());
            run.AppendChild(new Text(text));
            return;
        }

        textNodes[0].Text = text ?? string.Empty;
        for (int i = 1; i < textNodes.Count; i++)
        {
            textNodes[i].Text = string.Empty;
        }
    }

    /// <summary>
    /// Hard-reset a cell's content to a single paragraph/run with the given text.
    /// This is used for header metadata cells (Version, Date, Author, etc.) to
    /// prevent leftover inline labels from remaining in the cell.
    /// </summary>
    private static void ReplaceCellWithSingleParagraph(TableCell cell, string text)
    {
        cell.RemoveAllChildren();
        var paragraph = new Paragraph();
        var run = new Run();
        run.AppendChild(new Text(text ?? string.Empty));
        paragraph.AppendChild(run);
        cell.AppendChild(paragraph);
    }
}


