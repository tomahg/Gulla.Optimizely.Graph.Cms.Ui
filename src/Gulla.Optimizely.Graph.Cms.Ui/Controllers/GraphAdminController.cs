using System.Linq;
using Gulla.Optimizely.Graph.Cms.Ui.Configuration;
using Gulla.Optimizely.Graph.Cms.Ui.Services;
using Gulla.Optimizely.Graph.Cms.Ui.ViewModels;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gulla.Optimizely.Graph.Cms.Ui.Controllers
{
    [Route("GraphCmsUi")]
    [Authorize(Policy = GraphCmsUiAuthorizationPolicy.Default)]
    public class GraphAdminController : Controller
    {
        // Each tab needs its own path, not a ?tab= query parameter. The shell's left rail
        // matches menu items against the URL with the query string still attached, so
        // "/GraphCmsUi?tab=synonyms" never matches the current location "/GraphCmsUi" — and
        // with no matching item the rail treats itself as having no current section and stays
        // collapsed to bare icons. See GraphCmsUiMenuProvider.
        public const string PinnedResultsTab = "pinned-results";
        public const string SynonymsTab = "synonyms";

        private readonly ISiteCollectionResolver _resolver;
        private readonly IAntiforgery _antiforgery;

        public GraphAdminController(ISiteCollectionResolver resolver, IAntiforgery antiforgery)
        {
            _resolver = resolver;
            _antiforgery = antiforgery;
        }

        /// <summary>
        /// The bare page has no tab of its own, so it forwards to one. Kept because it is the
        /// addon's advertised entry point, and because <c>?tab=</c> links predate the routes
        /// below; a temporary redirect so a browser never caches the choice.
        /// </summary>
        [HttpGet("")]
        public IActionResult Index(string site = null, string lang = null, string tab = null)
        {
            return tab == SynonymsTab
                ? RedirectToAction(nameof(Synonyms), new { site, lang })
                : RedirectToAction(nameof(PinnedResults), new { site, lang });
        }

        [HttpGet(PinnedResultsTab)]
        public IActionResult PinnedResults(string site = null, string lang = null)
            => Page(PinnedResultsTab, site, lang);

        [HttpGet(SynonymsTab)]
        public IActionResult Synonyms(string site = null, string lang = null)
            => Page(SynonymsTab, site, lang);

        private IActionResult Page(string tab, string site, string lang)
        {
            // The CMS shell's React chrome (loaded via <platform-navigation-wrapper>) polls
            // /EPiServer/CMS/stores/notification via axios, which expects a XSRF-TOKEN cookie.
            // GetAndStoreTokens sets that cookie; without it the poll 400s with a cryptic
            // "AxiosError: Request failed with status code 400".
            _antiforgery.GetAndStoreTokens(HttpContext);

            var sites = _resolver.ListSites()
                .Select(s => new SiteOption { Key = s.Key, Name = s.Name })
                .ToList();

            var languages = _resolver.ListLanguages();

            var model = new GraphAdminViewModel
            {
                Sites = sites,
                Languages = languages,
                SelectedSiteKey = site ?? sites.FirstOrDefault()?.Key,
                SelectedLanguage = lang ?? _resolver.DefaultLanguage(),
                ActiveTab = tab
            };

            return View("Index", model);
        }
    }
}
