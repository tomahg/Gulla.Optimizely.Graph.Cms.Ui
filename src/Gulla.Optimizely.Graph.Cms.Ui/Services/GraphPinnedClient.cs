using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Gulla.Optimizely.Graph.Cms.Ui.Models;

namespace Gulla.Optimizely.Graph.Cms.Ui.Services
{
    public class GraphPinnedClient : IGraphPinnedClient
    {
        private readonly HttpClient _httpClient;

        public GraphPinnedClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        // ---- Collections ----

        public async Task<IReadOnlyList<PinnedCollection>> ListCollectionsAsync()
        {
            var response = await _httpClient.GetAsync("api/pinned/collections");
            await EnsureSuccessOrThrowWithBodyAsync(response);

            return await response.Content.ReadFromJsonAsync<List<PinnedCollection>>() ?? new List<PinnedCollection>();
        }

        public async Task<PinnedCollection> EnsureCollectionAsync(string key, string title)
        {
            var existing = (await ListCollectionsAsync())
                .FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase));

            return existing ?? await CreateCollectionAsync(key, title);
        }

        public async Task<PinnedCollection> CreateCollectionAsync(string key, string title)
        {
            var newCollection = new PinnedCollection
            {
                Title = title,
                Key = key,
                // Graph defaults a new collection to isActive=false, and an inactive collection
                // pins nothing. Always send true or the editor's first pin silently does nothing.
                IsActive = true
            };

            var response = await _httpClient.PostAsJsonAsync("api/pinned/collections", newCollection);
            await EnsureSuccessOrThrowWithBodyAsync(response);

            return await response.Content.ReadFromJsonAsync<PinnedCollection>();
        }

        public async Task DeleteCollectionAsync(string collectionId)
        {
            // Clear the items first. Graph documents DELETE /collections/{id}/items as "clear all
            // items", and doing it explicitly means the outcome doesn't depend on whether
            // deleting a collection cascades — which the API reference doesn't state either way.
            var clear = await _httpClient.DeleteAsync($"api/pinned/collections/{collectionId}/items");
            await EnsureSuccessOrThrowWithBodyAsync(clear);

            var response = await _httpClient.DeleteAsync($"api/pinned/collections/{collectionId}");
            await EnsureSuccessOrThrowWithBodyAsync(response);
        }

        // ---- Items ----

        /// <summary>
        /// Graph answers <c>GET api/pinned/collections/{id}/items</c> with at most 20 items, and
        /// offers no way to ask for more: <c>pageSize</c>, <c>limit</c>, <c>top</c>, <c>take</c>,
        /// <c>$top</c>, <c>count</c> and a dozen other spellings are all accepted and then
        /// silently ignored. <c>offset</c> is the one parameter it honours. Measured 2026-09-22
        /// against a collection of 159 items: offsets 0..140 answered 20, 20, 20, 20, 20, 20, 20,
        /// 19 — 159 items with 159 distinct ids. The page size is the server's, not ours; raising
        /// this number does nothing.
        /// </summary>
        private const int ItemPageSize = 20;

        /// <summary>
        /// Stops the paging loop if a server ever returns full pages without honouring
        /// <c>offset</c>, which would otherwise page forever. Far above any real collection.
        /// </summary>
        private const int MaxItemsPerCollection = 10000;

        public async Task<IReadOnlyList<PinnedResult>> ListAsync(string collectionId, string language)
        {
            // The response carries no total, no next link and no header saying it was cut off, so
            // a truncated page is indistinguishable from a whole collection. Page until a short
            // page comes back.
            var items = new List<PinnedResult>();

            for (var offset = 0; offset < MaxItemsPerCollection; offset += ItemPageSize)
            {
                var response = await _httpClient.GetAsync($"api/pinned/collections/{collectionId}/items?offset={offset}");
                await EnsureSuccessOrThrowWithBodyAsync(response);

                var page = await response.Content.ReadFromJsonAsync<List<PinnedResult>>();
                if (page == null || page.Count == 0)
                {
                    break;
                }

                items.AddRange(page);

                if (page.Count < ItemPageSize)
                {
                    break;
                }
            }

            // Filter once the whole set is in. Filtering per page would let a language vanish from
            // the list only because the first page happened to hold none of it.
            var normalized = LanguageNormalizer.ToIsoCode(language);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                // A null language means "every locale", so those items apply to the language
                // being filtered on and have to stay in the list — hiding them would let an
                // editor add a duplicate pin for a phrase that is already covered.
                items = items
                    .Where(i => i.Language == null
                             || string.Equals(i.Language, normalized, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            return items;
        }

        public async Task<PinnedResult> CreateAsync(string collectionId, PinnedResult item)
        {
            item.Language = LanguageNormalizer.ToIsoCode(item.Language);
            item.TargetKey = NormalizeTargetKey(item.TargetKey);

            var response = await _httpClient.PostAsJsonAsync($"api/pinned/collections/{collectionId}/items", item);
            await EnsureSuccessOrThrowWithBodyAsync(response);

            return await response.Content.ReadFromJsonAsync<PinnedResult>();
        }

        public async Task<PinnedResult> UpdateAsync(string collectionId, string itemId, PinnedResult item)
        {
            item.Language = LanguageNormalizer.ToIsoCode(item.Language);
            item.TargetKey = NormalizeTargetKey(item.TargetKey);

            var response = await _httpClient.PutAsJsonAsync($"api/pinned/collections/{collectionId}/items/{itemId}", item);
            await EnsureSuccessOrThrowWithBodyAsync(response);

            return await response.Content.ReadFromJsonAsync<PinnedResult>();
        }

        public async Task DeleteAsync(string collectionId, string itemId)
        {
            var response = await _httpClient.DeleteAsync($"api/pinned/collections/{collectionId}/items/{itemId}");
            await EnsureSuccessOrThrowWithBodyAsync(response);
        }

        /// <summary>
        /// Graph matches <c>targetKey</c> against the indexed document's <c>_metadata.key</c>, which
        /// is the content GUID in "N" format — 32 hex digits, no dashes. A dashed GUID is accepted
        /// and stored happily, then silently pins nothing: the query succeeds and simply ignores the
        /// item, so there is no error anywhere to point at the cause.
        /// </summary>
        private static string NormalizeTargetKey(string targetKey)
        {
            return Guid.TryParse(targetKey, out var guid) ? guid.ToString("N") : targetKey;
        }

        private static async Task EnsureSuccessOrThrowWithBodyAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var body = await response.Content.ReadAsStringAsync();

            // Carry Graph's status code on the exception so the API controller can pass it —
            // and the message — back to the UI instead of letting it surface as a 500.
            throw new HttpRequestException(
                $"Optimizely Graph returned {(int)response.StatusCode} {response.ReasonPhrase} for {response.RequestMessage?.RequestUri}. Body: {body}",
                null,
                response.StatusCode);
        }
    }
}
