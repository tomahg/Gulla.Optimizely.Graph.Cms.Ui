using System.Collections.Generic;
using System.Linq;
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
        private readonly GraphCmsUiOptions _options;

        public SiteCollectionResolver(
            IApplicationRepository applicationRepository,
            ILanguageBranchRepository languageRepository,
            IOptions<GraphCmsUiOptions> options)
        {
            _applicationRepository = applicationRepository;
            _languageRepository = languageRepository;
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

        public string DefaultSlot()
        {
            // Optimizely Graph only accepts a fixed set of slot names (documented as "one" and
            // "two"). Per-site scoping lives in pinned-result collections; synonyms have no
            // per-site dimension at all.
            return string.IsNullOrWhiteSpace(_options.DefaultSlot) ? "one" : _options.DefaultSlot;
        }
    }
}
