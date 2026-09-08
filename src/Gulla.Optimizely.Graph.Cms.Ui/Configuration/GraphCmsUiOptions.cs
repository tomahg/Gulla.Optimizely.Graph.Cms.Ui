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

        /// <summary>
        /// Menu section the Pinned Results and Synonyms items are placed under. The default is
        /// Optimizely Graph's own section, so the items appear at the bottom of its left menu
        /// (below GraphiQL, Admin and Content Sync) and the addon adds nothing to the product
        /// switcher. Null or empty gives the addon a top-level "Graph" entry of its own instead.
        /// That is also what happens when the Optimizely.ContentGraph.Cms package is not
        /// installed, since the default section would not exist.
        /// </summary>
        public string MenuParentPath { get; set; } = "/global/ContentGraph";
    }
}
