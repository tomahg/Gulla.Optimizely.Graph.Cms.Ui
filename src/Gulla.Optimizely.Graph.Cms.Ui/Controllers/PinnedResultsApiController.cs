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
        private readonly ISiteCollectionResolver _resolver;
        private readonly IContentLoader _contentLoader;
        private readonly IUrlResolver _urlResolver;

        public PinnedResultsApiController(
            IGraphPinnedClient pinnedClient,
            ISiteCollectionResolver resolver,
            IContentLoader contentLoader,
            IUrlResolver urlResolver)
        {
            _pinnedClient = pinnedClient;
            _resolver = resolver;
            _contentLoader = contentLoader;
            _urlResolver = urlResolver;
        }

        // ---- Collections ----

        public class CollectionView
        {
            /// <summary>Graph's collection id. Used by the REST API and by this UI.</summary>
            public string Id { get; set; }

            /// <summary>
            /// The collection key. This is what goes into a GraphQL
            /// <c>pinned: { collections: [...] }</c> argument — Graph matches on key there, not id.
            /// </summary>
            public string Key { get; set; }

            /// <summary>The key with its site suffix stripped, e.g. "black-friday".</summary>
            public string Name { get; set; }

            public string Title { get; set; }

            public bool IsActive { get; set; }

            /// <summary>The auto-created collection, which cannot be deleted.</summary>
            public bool IsDefault { get; set; }
        }

        [HttpGet("collections")]
        public async Task<IActionResult> ListCollections([FromQuery] string site)
        {
            if (string.IsNullOrWhiteSpace(site))
            {
                return BadRequest("site query parameter is required.");
            }

            try
            {
                // The default collection is created on demand so the picker is never empty and
                // an editor can pin something without first inventing a collection name.
                await _pinnedClient.EnsureCollectionAsync(
                    CollectionKeys.Build(CollectionKeys.DefaultName, site),
                    DefaultCollectionTitle(site));

                var all = await _pinnedClient.ListCollectionsAsync();
                return Ok(ToViews(all, site));
            }
            catch (HttpRequestException ex)
            {
                return GraphError(ex);
            }
        }

        public class CreateCollectionRequest
        {
            public string Name { get; set; }
        }

        [HttpPost("collections")]
        public async Task<IActionResult> CreateCollection([FromQuery] string site, [FromBody] CreateCollectionRequest body)
        {
            if (string.IsNullOrWhiteSpace(site))
            {
                return BadRequest("site query parameter is required.");
            }
            if (body == null || string.IsNullOrWhiteSpace(body.Name))
            {
                return BadRequest("Name is required.");
            }

            var name = CollectionKeys.Slug(body.Name);
            if (name.Length == 0)
            {
                return BadRequest("Name must contain at least one letter or digit.");
            }

            var key = CollectionKeys.Build(name, site);

            try
            {
                var existing = await _pinnedClient.ListCollectionsAsync();
                if (existing.Any(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase)))
                {
                    return Conflict($"A collection with the key '{key}' already exists.");
                }

                var created = await _pinnedClient.CreateCollectionAsync(key, $"{name} ({SiteDisplayName(site)})");
                return Ok(ToView(created, site));
            }
            catch (HttpRequestException ex)
            {
                return GraphError(ex);
            }
        }

        [HttpDelete("collections/{id}")]
        public async Task<IActionResult> DeleteCollection([FromRoute] string id, [FromQuery] string site)
        {
            if (string.IsNullOrWhiteSpace(site))
            {
                return BadRequest("site query parameter is required.");
            }

            try
            {
                var collection = (await _pinnedClient.ListCollectionsAsync())
                    .FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));

                if (collection == null)
                {
                    return NotFound("No such collection.");
                }

                // The default collection is recreated on the next page load, so "deleting" it
                // would really just empty it — a confusing outcome to offer as Delete.
                if (CollectionKeys.IsDefault(collection.Key, site))
                {
                    return BadRequest("The default collection cannot be deleted. Delete its pinned results instead.");
                }

                await _pinnedClient.DeleteCollectionAsync(id);
                return NoContent();
            }
            catch (HttpRequestException ex)
            {
                return GraphError(ex);
            }
        }

        // ---- Items ----

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] string collection, [FromQuery] string lang)
        {
            if (string.IsNullOrWhiteSpace(collection))
            {
                return BadRequest("collection query parameter is required.");
            }

            try
            {
                return Ok(await _pinnedClient.ListAsync(collection, lang));
            }
            catch (HttpRequestException ex)
            {
                return GraphError(ex);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromQuery] string collection, [FromBody] PinnedResult body)
        {
            if (string.IsNullOrWhiteSpace(collection))
            {
                return BadRequest("collection query parameter is required.");
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
                    created.Add(await _pinnedClient.CreateAsync(collection, WithPhrase(body, phrase)));
                }

                return Ok(created);
            }
            catch (HttpRequestException ex)
            {
                return GraphError(ex);
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update([FromRoute] string id, [FromQuery] string collection, [FromBody] PinnedResult body)
        {
            if (string.IsNullOrWhiteSpace(collection))
            {
                return BadRequest("collection query parameter is required.");
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
                var updated = await _pinnedClient.UpdateAsync(collection, id, WithPhrase(body, phrases[0]));
                for (var i = 1; i < phrases.Count; i++)
                {
                    await _pinnedClient.CreateAsync(collection, WithPhrase(body, phrases[i]));
                }

                return Ok(updated);
            }
            catch (HttpRequestException ex)
            {
                return GraphError(ex);
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete([FromRoute] string id, [FromQuery] string collection)
        {
            if (string.IsNullOrWhiteSpace(collection))
            {
                return BadRequest("collection query parameter is required.");
            }

            try
            {
                await _pinnedClient.DeleteAsync(collection, id);
                return NoContent();
            }
            catch (HttpRequestException ex)
            {
                return GraphError(ex);
            }
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

        private IEnumerable<CollectionView> ToViews(IEnumerable<PinnedCollection> all, string site)
        {
            var siteKeys = _resolver.ListSites().Select(s => s.Key).ToList();
            if (!siteKeys.Any(k => string.Equals(k, site, StringComparison.OrdinalIgnoreCase)))
            {
                siteKeys.Add(site);
            }

            return all
                .Where(c => string.Equals(OwningSite(c.Key, siteKeys), site, StringComparison.OrdinalIgnoreCase))
                .Select(c => ToView(c, site))
                // Default first, then alphabetically — the picker opens on the default.
                .OrderByDescending(v => v.IsDefault)
                .ThenBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// One site key can be a suffix of another — "site" and "my-site" both match a key
        /// ending in "-site" — so a collection belongs to the site with the LONGEST matching
        /// suffix, not to every site whose key it happens to end with.
        /// </summary>
        private static string OwningSite(string key, IEnumerable<string> siteKeys)
        {
            return siteKeys
                .Where(s => CollectionKeys.BelongsToSite(key, s))
                .OrderByDescending(s => CollectionKeys.Slug(s).Length)
                .FirstOrDefault();
        }

        private static CollectionView ToView(PinnedCollection c, string site)
        {
            var name = CollectionKeys.NameFrom(c.Key, site);
            return new CollectionView
            {
                Id = c.Id,
                Key = c.Key,
                Name = name,
                Title = c.Title,
                IsActive = c.IsActive,
                IsDefault = string.Equals(name, CollectionKeys.DefaultName, StringComparison.OrdinalIgnoreCase)
            };
        }

        private string DefaultCollectionTitle(string site)
        {
            return $"Pinned results for {SiteDisplayName(site)}";
        }

        private string SiteDisplayName(string site)
        {
            var match = _resolver.ListSites().FirstOrDefault(s => string.Equals(s.Key, site, StringComparison.OrdinalIgnoreCase));
            return string.IsNullOrWhiteSpace(match.Name) ? site : match.Name;
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
