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
        private readonly ISiteCollectionResolver _resolver;
        private readonly IAntiforgery _antiforgery;

        public GraphAdminController(ISiteCollectionResolver resolver, IAntiforgery antiforgery)
        {
            _resolver = resolver;
            _antiforgery = antiforgery;
        }

        [HttpGet("")]
        public IActionResult Index(string site = null, string lang = null, string tab = null)
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
                SelectedLanguage = lang ?? languages.FirstOrDefault(),
                ActiveTab = string.IsNullOrEmpty(tab) ? "best-bets" : tab
            };

            return View(model);
        }
    }
}
