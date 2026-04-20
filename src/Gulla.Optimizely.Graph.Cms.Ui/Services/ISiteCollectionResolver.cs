using System.Collections.Generic;
using System.Threading.Tasks;

namespace Gulla.Optimizely.Graph.Cms.Ui.Services
{
    public interface ISiteCollectionResolver
    {
        IReadOnlyList<(string Key, string Name)> ListSites();

        IReadOnlyList<string> ListLanguages();

        string CollectionKeyFor(string siteKey);

        string SlotFor(string siteKey);

        Task<string> EnsureCollectionIdAsync(string siteKey);
    }
}
