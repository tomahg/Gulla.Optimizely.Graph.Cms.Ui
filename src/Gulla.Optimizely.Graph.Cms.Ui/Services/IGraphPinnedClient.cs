using System.Collections.Generic;
using System.Threading.Tasks;
using Gulla.Optimizely.Graph.Cms.Ui.Models;

namespace Gulla.Optimizely.Graph.Cms.Ui.Services
{
    public interface IGraphPinnedClient
    {
        Task<IReadOnlyList<PinnedResult>> ListAsync(string siteKey, string language);

        Task<PinnedResult> CreateAsync(string siteKey, PinnedResult item);

        Task DeleteAsync(string siteKey, string itemId);
    }
}
