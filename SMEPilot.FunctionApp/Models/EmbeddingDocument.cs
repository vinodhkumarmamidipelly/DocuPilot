using System;
using MongoDB.Bson.Serialization.Attributes;

namespace SMEPilot.FunctionApp.Models
{
    public class EmbeddingDocument
    {
        [BsonId]
        [BsonElement("id")]
        public string id { get; set; }
        
        [BsonElement("TenantId")]
        public string TenantId { get; set; }
        
        [BsonElement("FileId")]
        public string FileId { get; set; }
        
        [BsonElement("FileUrl")]
        public string FileUrl { get; set; }
        
        [BsonElement("SectionId")]
        public string SectionId { get; set; }
        
        [BsonElement("Heading")]
        public string Heading { get; set; }
        
        [BsonElement("Summary")]
        public string Summary { get; set; }
        
        [BsonElement("Body")]
        public string Body { get; set; }
        
        [BsonElement("Embedding")]
        public float[] Embedding { get; set; }
        
        [BsonElement("CreatedAt")]
        public DateTime CreatedAt { get; set; }
    }
}

