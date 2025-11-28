using System;
using System.Collections.Generic;

namespace SMEPilot.FunctionApp.Models
{
    /// <summary>
    /// Structured DTOs for document extraction (Feedback1 recommendation)
    /// These provide better structure than simple text extraction
    /// </summary>
    
    /// <summary>
    /// Represents a paragraph with style information and position
    /// </summary>
    public class ParagraphDto
    {
        public int Index { get; init; }
        public string Text { get; init; } = "";
        public string? StyleId { get; init; }

        /// <summary>
        /// Optional list level for this paragraph (0 for top-level bullet/numbered items,
        /// 1 for first nested level, etc.). Null if the paragraph is not part of a list.
        /// </summary>
        public int? ListLevel { get; init; }
    }

    /// <summary>
    /// Represents an extracted image with anchor position
    /// </summary>
    public class ExtractedImage
    {
        public string Id { get; init; } = Guid.NewGuid().ToString();
        public byte[] Bytes { get; init; } = Array.Empty<byte>();
        public int AnchorParagraphIndex { get; init; } = 0;
    }

    /// <summary>
    /// Represents a table with rows and anchor position
    /// </summary>
    public class TableDto
    {
        public int AnchorParagraphIndex { get; set; }
        public List<List<string>> Rows { get; set; } = new();
    }

    /// <summary>
    /// Section model with confidence and mapping reasons (Feedback1)
    /// </summary>
    public class SectionModel
    {
        public string Heading { get; set; } = "";
        public List<string> Paragraphs { get; set; } = new();
        public List<TableDto> Tables { get; set; } = new();
        public List<ExtractedImage> Images { get; set; } = new();
        public double Confidence { get; set; }
        public List<string> MappingReasons { get; set; } = new();
    }
}

