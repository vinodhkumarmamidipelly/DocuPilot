using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SMEPilot.FunctionApp.Models
{
    /// <summary>
    /// Microsoft Graph change notification payload structure
    /// </summary>
    public class GraphChangeNotification
    {
        [JsonProperty("value")]
        public List<GraphNotificationItem> Value { get; set; }
        
        [JsonProperty("validationTokens")]
        public List<string> ValidationTokens { get; set; }
    }

    public class GraphNotificationItem
    {
        [JsonProperty("subscriptionId")]
        public string SubscriptionId { get; set; }
        
        [JsonProperty("changeType")]
        public string ChangeType { get; set; } // "created", "updated", "deleted"
        
        [JsonProperty("clientState")]
        public string ClientState { get; set; }
        
        [JsonProperty("resource")]
        public string Resource { get; set; }
        
        [JsonProperty("resourceData")]
        public GraphResourceData ResourceData { get; set; }
        
        [JsonProperty("encryptedContent")]
        public string EncryptedContent { get; set; } // Optional, for encrypted notifications
    }

    public class GraphResourceData
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        
        [JsonProperty("name")]
        public string Name { get; set; }
        
        [JsonProperty("driveId")]
        public string DriveId { get; set; }
        
        [JsonProperty("webUrl")]
        public string WebUrl { get; set; }
        
        [JsonProperty("@odata.type")]
        public string ODataType { get; set; } // "#Microsoft.Graph.driveItem"
        
        [JsonProperty("size")]
        public long? Size { get; set; }
        
        [JsonProperty("createdDateTime")]
        public DateTimeOffset? CreatedDateTime { get; set; }
        
        [JsonProperty("lastModifiedDateTime")]
        public DateTimeOffset? LastModifiedDateTime { get; set; }
        
        [JsonProperty("createdBy")]
        public GraphUserInfo CreatedBy { get; set; }
    }

    public class GraphUserInfo
    {
        [JsonProperty("user")]
        public GraphUser User { get; set; }
    }

    public class GraphUser
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        
        [JsonProperty("displayName")]
        public string DisplayName { get; set; }
        
        [JsonProperty("email")]
        public string Email { get; set; }
    }
}

