namespace SMEPilot.FunctionApp.Models
{
    public class SharePointEvent
    {
        public string siteId { get; set; }
        public string driveId { get; set; }
        public string itemId { get; set; }
        public string fileName { get; set; }
        public string uploaderEmail { get; set; }
        public string tenantId { get; set; }
    }
}

