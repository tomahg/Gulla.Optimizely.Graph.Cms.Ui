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

        // Graph types this as a double and enforces NO bounds — verified against
        // cg.optimizely.com 2026-09-08, which accepted and stored 0, -1000, 0.5, 2.5,
        // 2147483648 and ±Double.MaxValue, rejecting only non-numeric JSON (400
        // VALIDATION_ERROR). An omitted priority defaults to 1000 server-side, while an
        // explicit null is stored AS null, so the field is genuinely nullable.
        //
        // Typing it as int therefore broke reads: System.Text.Json throws on a decimal, on a
        // value past int.MaxValue and on null, and because the listing deserializes the whole
        // collection at once, one such item failed every item. Reachable whenever a pin is
        // written outside this UI — Optimizely's own Search Management portal, a migration
        // script, the REST API by hand. The form still offers whole numbers; this is about
        // reading back faithfully what Graph will hand us.
        [JsonPropertyName("priority")]
        public double? Priority { get; set; } = 1;

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; } = true;
    }
}
