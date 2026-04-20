using System.Text.Json.Serialization;

namespace Gulla.Optimizely.Graph.Cms.Ui.Models
{
    public class PinnedResult
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("phrases")]
        public string Phrases { get; set; }

        [JsonPropertyName("targetKey")]
        public string TargetKey { get; set; }

        [JsonPropertyName("language")]
        public string Language { get; set; }

        [JsonPropertyName("priority")]
        public int Priority { get; set; } = 1;

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; } = true;
    }
}
