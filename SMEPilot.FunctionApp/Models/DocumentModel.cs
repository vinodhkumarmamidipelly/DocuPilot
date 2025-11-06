using System.Collections.Generic;

namespace SMEPilot.FunctionApp.Models
{
    public class DocumentModel
    {
        public string Title { get; set; }
        public List<Section> Sections { get; set; } = new();
        public List<ImageData> Images { get; set; } = new();
    }

    public class Section
    {
        public string Id { get; set; }
        public string Heading { get; set; }
        public string Summary { get; set; }
        public string Body { get; set; }
    }

    public class ImageData
    {
        public string Id { get; set; }
        public string Alt { get; set; }
        public byte[] Bytes { get; set; }
    }
}

