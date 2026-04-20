using System.Text.Json.Serialization;

namespace Gulla.Optimizely.Graph.Cms.Ui.Models
{
    public class PinnedCollection
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("key")]
        public string Key { get; set; }

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; } = true;
    }
}
