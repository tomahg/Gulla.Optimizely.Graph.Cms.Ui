using System.Collections.Generic;
using System.Threading.Tasks;
using Gulla.Optimizely.Graph.Cms.Ui.Models;

namespace Gulla.Optimizely.Graph.Cms.Ui.Services
{
    public interface IGraphPinnedClient
    {
        Task<IReadOnlyList<PinnedCollection>> ListCollectionsAsync();

        /// <summary>Returns the collection with this key, creating it if it doesn't exist yet.</summary>
        Task<PinnedCollection> EnsureCollectionAsync(string key, string title);

        /// <summary>Creates a collection. Fails if the key is already taken.</summary>
        Task<PinnedCollection> CreateCollectionAsync(string key, string title);

        /// <summary>Deletes a collection and every pinned result in it.</summary>
        Task DeleteCollectionAsync(string collectionId);

        Task<IReadOnlyList<PinnedResult>> ListAsync(string collectionId, string language);

        Task<PinnedResult> CreateAsync(string collectionId, PinnedResult item);

        Task<PinnedResult> UpdateAsync(string collectionId, string itemId, PinnedResult item);

        Task DeleteAsync(string collectionId, string itemId);
    }
}
