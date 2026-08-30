using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Gulla.Optimizely.Graph.Cms.Ui.Configuration;
using Gulla.Optimizely.Graph.Cms.Ui.Models;
using Microsoft.Extensions.Options;

namespace Gulla.Optimizely.Graph.Cms.Ui.Services
{
    public class GraphPinnedClient : IGraphPinnedClient
    {
        private readonly HttpClient _httpClient;
        private readonly GraphCmsUiOptions _options;
        private readonly ConcurrentDictionary<string, string> _collectionIdByKey = new();

        public GraphPinnedClient(HttpClient httpClient, IOptions<GraphCmsUiOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value;
        }

        public async Task<IReadOnlyList<PinnedResult>> ListAsync(string siteKey, string language)
        {
            var collectionId = await EnsureCollectionAsync(BuildCollectionKey(siteKey), $"Pinned results for {siteKey}");
            var response = await _httpClient.GetAsync($"api/pinned/collections/{collectionId}/items");
            await EnsureSuccessOrThrowWithBodyAsync(response);

            var items = await response.Content.ReadFromJsonAsync<List<PinnedResult>>() ?? new List<PinnedResult>();

            var normalized = LanguageNormalizer.ToIsoCode(language);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                items = items.Where(i => string.Equals(i.Language, normalized, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return items;
        }

        public async Task<PinnedResult> CreateAsync(string siteKey, PinnedResult item)
        {
            item.Language = LanguageNormalizer.ToIsoCode(item.Language);
            item.TargetKey = NormalizeTargetKey(item.TargetKey);
            var collectionId = await EnsureCollectionAsync(BuildCollectionKey(siteKey), $"Pinned results for {siteKey}");
            var response = await _httpClient.PostAsJsonAsync($"api/pinned/collections/{collectionId}/items", item);
            await EnsureSuccessOrThrowWithBodyAsync(response);

            return await response.Content.ReadFromJsonAsync<PinnedResult>();
        }

        public async Task<PinnedResult> UpdateAsync(string siteKey, string itemId, PinnedResult item)
        {
            item.Language = LanguageNormalizer.ToIsoCode(item.Language);
            item.TargetKey = NormalizeTargetKey(item.TargetKey);
            var collectionId = await EnsureCollectionAsync(BuildCollectionKey(siteKey), $"Pinned results for {siteKey}");
            var response = await _httpClient.PutAsJsonAsync($"api/pinned/collections/{collectionId}/items/{itemId}", item);
            await EnsureSuccessOrThrowWithBodyAsync(response);

            return await response.Content.ReadFromJsonAsync<PinnedResult>();
        }

        public async Task DeleteAsync(string siteKey, string itemId)
        {
            var collectionId = await EnsureCollectionAsync(BuildCollectionKey(siteKey), $"Pinned results for {siteKey}");
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

        public async Task<string> EnsureCollectionAsync(string key, string title)
        {
            if (_collectionIdByKey.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var existing = await FindCollectionAsync(key);
            if (existing != null)
            {
                _collectionIdByKey[key] = existing.Id;
                return existing.Id;
            }

            var newCollection = new PinnedCollection
            {
                Title = title,
                Key = key,
                IsActive = true
            };

            var response = await _httpClient.PostAsJsonAsync("api/pinned/collections", newCollection);
            await EnsureSuccessOrThrowWithBodyAsync(response);

            var created = await response.Content.ReadFromJsonAsync<PinnedCollection>();
            _collectionIdByKey[key] = created.Id;
            return created.Id;
        }

        private async Task<PinnedCollection> FindCollectionAsync(string key)
        {
            var response = await _httpClient.GetAsync("api/pinned/collections");
            await EnsureSuccessOrThrowWithBodyAsync(response);

            var collections = await response.Content.ReadFromJsonAsync<List<PinnedCollection>>() ?? new List<PinnedCollection>();
            return collections.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase));
        }

        private string BuildCollectionKey(string siteKey)
        {
            var prefix = string.IsNullOrWhiteSpace(_options.CollectionKeyPrefix) ? "gulla" : _options.CollectionKeyPrefix;
            return $"{prefix}-{siteKey}".ToLowerInvariant();
        }
    }
}
