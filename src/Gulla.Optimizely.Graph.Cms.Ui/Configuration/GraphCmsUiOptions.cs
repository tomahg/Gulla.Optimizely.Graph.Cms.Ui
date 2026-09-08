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

        /// <summary>
        /// Language pre-selected in the UI's language picker when the URL doesn't name one.
        /// Either a CMS language ID ("nb-NO") or a bare ISO code ("nb") that is matched against
        /// the enabled languages. Ignored when it doesn't match an enabled language; null or
        /// empty means "the first enabled language".
        /// </summary>
        public string DefaultLanguage { get; set; }
    }
}
