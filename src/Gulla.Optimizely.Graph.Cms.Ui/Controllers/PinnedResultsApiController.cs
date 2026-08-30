using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
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

            var phrases = SplitPhrases(body.Phrases);
            if (phrases.Count == 0)
            {
                return BadRequest("Phrases and TargetKey are required.");
            }

            try
            {
                var created = new List<PinnedResult>();
                foreach (var phrase in phrases)
                {
                    created.Add(await _pinnedClient.CreateAsync(site, WithPhrase(body, phrase)));
                }

                return Ok(created);
            }
            catch (HttpRequestException ex)
            {
                return GraphError(ex);
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update([FromRoute] string id, [FromQuery] string site, [FromBody] PinnedResult body)
        {
            if (string.IsNullOrWhiteSpace(site))
            {
                return BadRequest("site query parameter is required.");
            }
            if (body == null || string.IsNullOrWhiteSpace(body.Phrases) || string.IsNullOrWhiteSpace(body.TargetKey))
            {
                return BadRequest("Phrases and TargetKey are required.");
            }

            var phrases = SplitPhrases(body.Phrases);
            if (phrases.Count == 0)
            {
                return BadRequest("Phrases and TargetKey are required.");
            }

            try
            {
                // One pinned item holds one phrase, so an edit that adds phrases updates this
                // item with the first and adds the rest alongside it.
                var updated = await _pinnedClient.UpdateAsync(site, id, WithPhrase(body, phrases[0]));
                for (var i = 1; i < phrases.Count; i++)
                {
                    await _pinnedClient.CreateAsync(site, WithPhrase(body, phrases[i]));
                }

                return Ok(updated);
            }
            catch (HttpRequestException ex)
            {
                return GraphError(ex);
            }
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
                // "N" (no dashes) is the form Graph indexes as _metadata.key and matches
                // pinned targetKey against — see GraphPinnedClient.NormalizeTargetKey.
                ContentGuid = content.ContentGuid.ToString("N"),
                Name = content.Name,
                Url = SafeUrl(content.ContentLink)
            });
        }

        /// <summary>
        /// Graph matches <c>phrases</c> as one literal string — commas and all — so a pinned item
        /// holds exactly one phrase. "a,b,c" stored as-is fires only for a search for the whole
        /// string "a,b,c" and never for a, b or c on their own, silently. The comma-separated input
        /// the UI offers therefore has to become one item per phrase.
        /// </summary>
        private static List<string> SplitPhrases(string phrases)
        {
            if (string.IsNullOrWhiteSpace(phrases))
            {
                return new List<string>();
            }

            return phrases
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static PinnedResult WithPhrase(PinnedResult source, string phrase)
        {
            return new PinnedResult
            {
                Phrases = phrase,
                TargetKey = source.TargetKey,
                Language = source.Language,
                Priority = source.Priority,
                IsActive = source.IsActive
            };
        }

        /// <summary>
        /// Passes a Graph failure back to the UI with Graph's own status code and message —
        /// a duplicate phrase/target/language combination comes back as 409 with an explanation
        /// worth showing the editor, and an unhandled exception would only produce a bare 500.
        /// </summary>
        private IActionResult GraphError(HttpRequestException ex)
        {
            return StatusCode((int)(ex.StatusCode ?? HttpStatusCode.BadGateway), ex.Message);
        }

        private string SafeUrl(ContentReference contentLink)
        {
            try { return _urlResolver.GetUrl(contentLink); }
            catch { return null; }
        }
    }
}
