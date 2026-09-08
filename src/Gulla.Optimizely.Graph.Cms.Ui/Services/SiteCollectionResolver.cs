using System;
using System.Collections.Generic;
using System.Linq;
using EPiServer.DataAbstraction;
using EPiServer.Web;
using Gulla.Optimizely.Graph.Cms.Ui.Configuration;
using Microsoft.Extensions.Options;

namespace Gulla.Optimizely.Graph.Cms.Ui.Services
{
    public class SiteCollectionResolver : ISiteCollectionResolver
    {
        private readonly ISiteDefinitionRepository _siteDefinitionRepository;
        private readonly ILanguageBranchRepository _languageRepository;
        private readonly GraphCmsUiOptions _options;

        public SiteCollectionResolver(
            ISiteDefinitionRepository siteDefinitionRepository,
            ILanguageBranchRepository languageRepository,
            IOptions<GraphCmsUiOptions> options)
        {
            _siteDefinitionRepository = siteDefinitionRepository;
            _languageRepository = languageRepository;
            _options = options.Value;
        }

        public IReadOnlyList<(string Key, string Name)> ListSites()
        {
            return _siteDefinitionRepository.List()
                .Select(s => (Key: s.Name, Name: s.Name))
                .ToList();
        }

        public IReadOnlyList<string> ListLanguages()
        {
            return _languageRepository.ListEnabled()
                .Select(l => l.LanguageID)
                .ToList();
        }

        public string DefaultSlot()
        {
            // Optimizely Graph only accepts a fixed set of slot names (documented as "one" and
            // "two"). Per-site scoping lives in pinned-result collections; synonyms have no
            // per-site dimension at all.
            return string.IsNullOrWhiteSpace(_options.DefaultSlot) ? "one" : _options.DefaultSlot;
        }

        /// <summary>
        /// Resolves the configured default language against the enabled ones. Matching is
        /// forgiving in both directions — the CMS deals in culture codes ("nb-NO") while Graph,
        /// and therefore anyone reading Graph's docs, deals in ISO codes ("nb") — so a configured
        /// "nb" selects the enabled "nb-NO" and vice versa. Anything that still doesn't match an
        /// enabled language falls back to the first one, so the picker never renders with
        /// nothing selected.
        /// </summary>
        public string DefaultLanguage()
        {
            var languages = ListLanguages();

            if (string.IsNullOrWhiteSpace(_options.DefaultLanguage))
            {
                return languages.FirstOrDefault();
            }

            var configured = _options.DefaultLanguage.Trim();

            var exact = languages.FirstOrDefault(l => string.Equals(l, configured, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
            {
                return exact;
            }

            var iso = LanguageNormalizer.ToIsoCode(configured);

            return languages.FirstOrDefault(l => LanguageNormalizer.ToIsoCode(l) == iso)
                   ?? languages.FirstOrDefault();
        }
    }
}
