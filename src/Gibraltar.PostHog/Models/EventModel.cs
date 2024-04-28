using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Gibraltar.PostHog.Models
{
    /// <summary>
    /// PostHog's raw event data model
    /// </summary>
    internal class EventModel
    {
        [JsonPropertyName("event")]
        public string Name { get; set; }

        [JsonPropertyName("api_key")]
        public string ApiKey { get; set; }

        [JsonPropertyName("distinct_id")]
        public string UserKey { get; set; }

        [JsonPropertyName("properties")]
        public Dictionary<string, object> Properties { get; set; }

        [JsonPropertyName("timestamp")]
        public DateTimeOffset Timestamp { get; set; }
    }
}
