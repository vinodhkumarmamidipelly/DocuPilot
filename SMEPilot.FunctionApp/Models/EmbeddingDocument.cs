using System;

namespace SMEPilot.FunctionApp.Models
{
    public class EmbeddingDocument
    {
        public string id { get; set; }
        public string TenantId { get; set; }
        public string FileId { get; set; }
        public string FileUrl { get; set; }
        public string SectionId { get; set; }
        public string Heading { get; set; }
        public string Summary { get; set; }
        public string Body { get; set; }
        public float[] Embedding { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

