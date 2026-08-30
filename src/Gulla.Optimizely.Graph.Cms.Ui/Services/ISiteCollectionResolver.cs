using System.Collections.Generic;

namespace Gulla.Optimizely.Graph.Cms.Ui.Services
{
    public interface ISiteCollectionResolver
    {
        IReadOnlyList<(string Key, string Name)> ListSites();

        IReadOnlyList<string> ListLanguages();

        /// <summary>
        /// The synonym slot used when the caller doesn't name one. Graph scopes synonyms by
        /// language and slot only — there is no per-site dimension to resolve.
        /// </summary>
        string DefaultSlot();
    }
}
