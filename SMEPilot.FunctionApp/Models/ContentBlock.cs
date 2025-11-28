using System;
using System.Collections.Generic;

namespace SMEPilot.FunctionApp.Models
{
    /// <summary>
    /// ContentBlock model from Feedback2 - simpler block-based approach
    /// </summary>
    public enum BlockType { Heading, Paragraph, List, Table, Image, Unknown }

    /// <summary>
    /// Represents a content block in a document
    /// </summary>
    public class ContentBlock
    {
        public BlockType Type { get; set; }
        public string Text { get; set; } = "";
        public byte[] ImageBytes { get; set; } = Array.Empty<byte>();
        public int HeadingLevel { get; set; } = 0;
        public int Position { get; set; }
    }

    /// <summary>
    /// Normalized document with blocks (Feedback2)
    /// </summary>
    public class NormalizedDocument
    {
        public string Title { get; set; } = "";
        public string DetectedType { get; set; } = "";
        public List<ContentBlock> Blocks { get; set; } = new List<ContentBlock>();
    }

    /// <summary>
    /// Mapped section with blocks (Feedback2)
    /// </summary>
    public class MappedSection
    {
        public string SectionName { get; set; } = "";
        public List<ContentBlock> Blocks { get; set; } = new List<ContentBlock>();
    }

    /// <summary>
    /// Document context for enrichment (Feedback2)
    /// </summary>
    public class DocumentContext
    {
        public string LocalSourcePath { get; set; } = "";
        public string FileName { get; set; } = "";
        public string TemplatePath { get; set; } = "";
        public string MappingJsonPath { get; set; } = "";
        public long MaxFileSizeBytes { get; set; } = 50 * 1024 * 1024; // default 50MB
    }
}

