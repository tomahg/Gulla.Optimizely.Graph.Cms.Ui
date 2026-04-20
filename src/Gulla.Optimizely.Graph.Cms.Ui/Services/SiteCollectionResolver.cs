using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EPiServer.Applications;
using EPiServer.DataAbstraction;
using Gulla.Optimizely.Graph.Cms.Ui.Configuration;
using Microsoft.Extensions.Options;

namespace Gulla.Optimizely.Graph.Cms.Ui.Services
{
    public class SiteCollectionResolver : ISiteCollectionResolver
    {
        private readonly IApplicationRepository _applicationRepository;
        private readonly ILanguageBranchRepository _languageRepository;
        private readonly IGraphPinnedClient _pinnedClient;
        private readonly GraphCmsUiOptions _options;
        private readonly ConcurrentDictionary<string, string> _collectionIdBySite = new();

        public SiteCollectionResolver(
            IApplicationRepository applicationRepository,
            ILanguageBranchRepository languageRepository,
            IGraphPinnedClient pinnedClient,
            IOptions<GraphCmsUiOptions> options)
        {
            _applicationRepository = applicationRepository;
            _languageRepository = languageRepository;
            _pinnedClient = pinnedClient;
            _options = options.Value;
        }

        public IReadOnlyList<(string Key, string Name)> ListSites()
        {
            return _applicationRepository.List()
                .Select(a => (Key: a.Name, Name: string.IsNullOrWhiteSpace(a.DisplayName) ? a.Name : a.DisplayName))
                .ToList();
        }

        public IReadOnlyList<string> ListLanguages()
        {
            return _languageRepository.ListEnabled()
                .Select(l => l.LanguageID)
                .ToList();
        }

        public string CollectionKeyFor(string siteKey)
        {
            var prefix = string.IsNullOrWhiteSpace(_options.CollectionKeyPrefix) ? "gulla" : _options.CollectionKeyPrefix;
            return $"{prefix}-{Sanitize(siteKey)}";
        }

        public string SlotFor(string siteKey)
        {
            // Optimizely Graph only accepts a fixed set of slot names (documented as "one" and "two").
            // Per-site scoping lives in pinned-result collections; synonyms use the configured default slot.
            return string.IsNullOrWhiteSpace(_options.DefaultSlot) ? "one" : _options.DefaultSlot;
        }

        public async Task<string> EnsureCollectionIdAsync(string siteKey)
        {
            if (_collectionIdBySite.TryGetValue(siteKey, out var cached))
            {
                return cached;
            }

            var collectionKey = CollectionKeyFor(siteKey);
            var id = await ResolveOrCreateAsync(siteKey, collectionKey);
            _collectionIdBySite[siteKey] = id;
            return id;
        }

        private Task<string> ResolveOrCreateAsync(string siteKey, string collectionKey)
        {
            if (_pinnedClient is GraphPinnedClient concrete)
            {
                return concrete.EnsureCollectionAsync(collectionKey, BuildCollectionTitle(siteKey));
            }

            throw new InvalidOperationException("IGraphPinnedClient must support EnsureCollectionAsync.");
        }

        private string BuildCollectionTitle(string siteKey)
        {
            var app = _applicationRepository.List().FirstOrDefault(a => string.Equals(a.Name, siteKey, StringComparison.OrdinalIgnoreCase));
            var displayName = app != null ? (string.IsNullOrWhiteSpace(app.DisplayName) ? app.Name : app.DisplayName) : siteKey;
            return $"Pinned results for {displayName}";
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);
            foreach (var c in value.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(c);
                }
                else if (c == '-' || c == '_')
                {
                    builder.Append('-');
                }
            }
            return builder.ToString();
        }
    }
}
