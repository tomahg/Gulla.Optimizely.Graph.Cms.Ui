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

        public async Task<IReadOnlyList<PinnedResult>> ListAsync(string collectionId, string language)
        {
            var response = await _httpClient.GetAsync($"api/pinned/collections/{collectionId}/items");
            await EnsureSuccessOrThrowWithBodyAsync(response);

            var items = await response.Content.ReadFromJsonAsync<List<PinnedResult>>() ?? new List<PinnedResult>();

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
