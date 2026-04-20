namespace Gulla.Optimizely.Graph.Cms.Ui.Configuration
{
    public class GraphCmsUiOptions
    {
        public string GatewayAddress { get; set; } = "https://cg.optimizely.com";

        public string AppKey { get; set; }

        public string Secret { get; set; }

        public string SingleKey { get; set; }

        public string CollectionKeyPrefix { get; set; } = "gulla";

        public string DefaultSlot { get; set; } = "one";
    }
}
