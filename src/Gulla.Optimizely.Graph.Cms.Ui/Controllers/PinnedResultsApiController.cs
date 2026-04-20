using System;
using System.Threading.Tasks;
using EPiServer;
using EPiServer.Core;
using EPiServer.Web.Routing;
using Gulla.Optimizely.Graph.Cms.Ui.Configuration;
using Gulla.Optimizely.Graph.Cms.Ui.Models;
using Gulla.Optimizely.Graph.Cms.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gulla.Optimizely.Graph.Cms.Ui.Controllers
{
    [Route("GraphCmsUi/api/pinned")]
    [Authorize(Policy = GraphCmsUiAuthorizationPolicy.Default)]
    [ApiController]
    public class PinnedResultsApiController : ControllerBase
    {
        private readonly IGraphPinnedClient _pinnedClient;
        private readonly IContentLoader _contentLoader;
        private readonly IUrlResolver _urlResolver;

        public PinnedResultsApiController(
            IGraphPinnedClient pinnedClient,
            IContentLoader contentLoader,
            IUrlResolver urlResolver)
        {
            _pinnedClient = pinnedClient;
            _contentLoader = contentLoader;
            _urlResolver = urlResolver;
        }

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] string site, [FromQuery] string lang)
        {
            if (string.IsNullOrWhiteSpace(site))
            {
                return BadRequest("site query parameter is required.");
            }

            var items = await _pinnedClient.ListAsync(site, lang);
            return Ok(items);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromQuery] string site, [FromBody] PinnedResult body)
        {
            if (string.IsNullOrWhiteSpace(site))
            {
                return BadRequest("site query parameter is required.");
            }
            if (body == null || string.IsNullOrWhiteSpace(body.Phrases) || string.IsNullOrWhiteSpace(body.TargetKey))
            {
                return BadRequest("Phrases and TargetKey are required.");
            }

            var created = await _pinnedClient.CreateAsync(site, body);
            return Ok(created);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete([FromRoute] string id, [FromQuery] string site)
        {
            if (string.IsNullOrWhiteSpace(site))
            {
                return BadRequest("site query parameter is required.");
            }

            await _pinnedClient.DeleteAsync(site, id);
            return NoContent();
        }

        public class ContentMatch
        {
            public string ContentGuid { get; set; }
            public string Name { get; set; }
            public string Url { get; set; }
        }

        [HttpGet("resolve-content")]
        public IActionResult ResolveContent([FromQuery] string guid = null, [FromQuery] string contentLink = null)
        {
            IContent content = null;

            if (!string.IsNullOrWhiteSpace(guid) && Guid.TryParse(guid, out var parsedGuid))
            {
                _contentLoader.TryGet<IContent>(parsedGuid, out content);
            }
            else if (!string.IsNullOrWhiteSpace(contentLink) && ContentReference.TryParse(contentLink, out var parsedRef) && !ContentReference.IsNullOrEmpty(parsedRef))
            {
                _contentLoader.TryGet<IContent>(parsedRef, out content);
            }
            else
            {
                return BadRequest("Either guid or contentLink query parameter is required.");
            }

            if (content == null)
            {
                return NotFound();
            }

            return Ok(new ContentMatch
            {
                ContentGuid = content.ContentGuid.ToString(),
                Name = content.Name,
                Url = SafeUrl(content.ContentLink)
            });
        }

        private string SafeUrl(ContentReference contentLink)
        {
            try { return _urlResolver.GetUrl(contentLink); }
            catch { return null; }
        }
    }
}
