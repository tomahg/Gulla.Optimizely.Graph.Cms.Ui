namespace Gulla.Optimizely.Graph.Cms.Ui.Configuration
{
    public class GraphCmsUiOptions
    {
        public string GatewayAddress { get; set; } = "https://cg.optimizely.com";

        public string AppKey { get; set; }

        public string Secret { get; set; }

        public string SingleKey { get; set; }

        /// <summary>
        /// Synonym slot used when a request doesn't name one. Graph accepts "one" or "two".
        /// </summary>
        public string DefaultSlot { get; set; } = "one";
    }
}
