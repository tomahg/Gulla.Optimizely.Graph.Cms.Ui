using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Gulla.Optimizely.Graph.Cms.Ui.Configuration;
using Gulla.Optimizely.Graph.Cms.Ui.Models;
using Gulla.Optimizely.Graph.Cms.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Gulla.Optimizely.Graph.Cms.Ui.Controllers
{
    [Route("GraphCmsUi/api/synonyms")]
    [Authorize(Policy = GraphCmsUiAuthorizationPolicy.Default)]
    [ApiController]
    public class SynonymsApiController : ControllerBase
    {
        private readonly IGraphSynonymClient _synonymClient;
        private readonly ISiteCollectionResolver _resolver;
        private readonly SynonymCsvSerializer _csv;

        public SynonymsApiController(
            IGraphSynonymClient synonymClient,
            ISiteCollectionResolver resolver,
            SynonymCsvSerializer csv)
        {
            _synonymClient = synonymClient;
            _resolver = resolver;
            _csv = csv;
        }

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] string lang, [FromQuery] string slot = null)
        {
            if (string.IsNullOrWhiteSpace(lang))
            {
                return BadRequest("lang query parameter is required.");
            }

            try
            {
                var body = await _synonymClient.GetRawAsync(ResolveSlot(slot), SynonymLanguage.RouteFor(lang));
                return Ok(_csv.ParseGraphBody(body));
            }
            catch (HttpRequestException ex)
            {
                return GraphError(ex);
            }
        }

        public class LanguageShareView
        {
            /// <summary>The CMS language id, as shown in the language picker.</summary>
            public string Id { get; set; }

            /// <summary>The ISO code sent to Graph as <c>language_routing</c>.</summary>
            public string Route { get; set; }

            /// <summary>Other CMS languages served the same synonym list.</summary>
            public List<string> SharedWith { get; set; } = new List<string>();

            /// <summary>
            /// False when the sharing is certain because the languages resolve to the same
            /// route; true when it was inferred from two routes returning identical content.
            /// </summary>
            public bool Inferred { get; set; }

            /// <summary>
            /// Whether the answer is trustworthy either way. An empty list cannot be told apart
            /// from another empty list on a different route — they may be one shared document or
            /// two separate ones — so an empty list alongside other empty lists is inconclusive.
            /// A non-empty list is always conclusive: content that differs cannot be the same
            /// document, and an empty list cannot be the same document as a non-empty one.
            /// </summary>
            public bool Conclusive { get; set; }
        }

        /// <summary>
        /// Which enabled languages are actually served the same synonym list.
        /// <para>
        /// Graph folds related variants together — <c>no</c>, <c>nb</c>, <c>nn</c> and
        /// <c>nn-NO</c> all address one document, verified against a live instance — and nothing
        /// in the API or the documentation says which codes collapse. A picker built from the
        /// CMS language list therefore offers one entry per language while several of them edit
        /// the same list, so the sharing has to be measured rather than assumed.
        /// </para>
        /// <para>
        /// Two languages are reported as sharing when they resolve to the same route (certain),
        /// or when two different routes return identical non-empty content (inferred). Empty
        /// lists are never grouped across routes: two genuinely separate empty lists look alike.
        /// </para>
        /// </summary>
        [HttpGet("languages")]
        public async Task<IActionResult> Languages([FromQuery] string slot = null)
        {
            var resolvedSlot = ResolveSlot(slot);
            var languages = _resolver.ListLanguages().Select(TargetLanguage.For)
                .Where(t => !string.IsNullOrWhiteSpace(t.Route))
                .ToList();

            try
            {
                var bodyByRoute = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var route in languages.Select(l => l.Route).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    bodyByRoute[route] = (await _synonymClient.GetRawAsync(resolvedSlot, route) ?? string.Empty).Trim();
                }

                var views = languages.Select(l => new LanguageShareView { Id = l.Id, Route = l.Route }).ToList();

                foreach (var view in views)
                {
                    var body = bodyByRoute[view.Route];
                    foreach (var other in views.Where(v => v.Id != view.Id))
                    {
                        var sameRoute = string.Equals(other.Route, view.Route, StringComparison.OrdinalIgnoreCase);
                        var sameContent = body.Length > 0 && body == bodyByRoute[other.Route];
                        if (!sameRoute && !sameContent)
                        {
                            continue;
                        }

                        view.SharedWith.Add(other.Id);
                        view.Inferred |= !sameRoute;
                    }

                    view.Conclusive = body.Length > 0
                        || !views.Any(v => v.Id != view.Id
                            && !string.Equals(v.Route, view.Route, StringComparison.OrdinalIgnoreCase)
                            && bodyByRoute[v.Route].Length == 0);
                }

                return Ok(views);
            }
            catch (HttpRequestException ex)
            {
                return GraphError(ex);
            }
        }

        public class CreateSynonymRequest
        {
            public string Phrases { get; set; }
            public string Synonym { get; set; }
            public bool Bidirectional { get; set; }
        }

        /// <summary>
        /// Graph has no all-languages synonym list — each language is a separate document, and
        /// the list stored without <c>language_routing</c> (see <see cref="SynonymLanguage"/>)
        /// applies only to queries without a locale, not to every locale. <paramref name="allLanguages"/>
        /// is therefore a write-time fan-out, not a scope: it writes the same rule into every
        /// enabled language's slot, and the copies are independent from that moment on.
        /// Each language is written separately and can fail separately, so the response reports
        /// per-language outcomes rather than a single success or failure.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create(
            [FromQuery] string lang,
            [FromQuery] string slot,
            [FromQuery] bool allLanguages,
            [FromBody] CreateSynonymRequest body)
        {
            if (string.IsNullOrWhiteSpace(lang) && !allLanguages)
            {
                return BadRequest("lang query parameter is required.");
            }
            if (body == null || string.IsNullOrWhiteSpace(body.Phrases) || string.IsNullOrWhiteSpace(body.Synonym))
            {
                return BadRequest("Phrases and Synonym are required.");
            }

            var newEntry = new SynonymEntry
            {
                Phrases = body.Phrases.Split(',').Select(p => p.Trim()).Where(p => p.Length > 0).ToList(),
                Synonym = body.Synonym.Trim(),
                Bidirectional = body.Bidirectional
            };

            if (newEntry.Phrases.Count == 0)
            {
                return BadRequest("At least one phrase is required.");
            }

            var resolvedSlot = ResolveSlot(slot);

            // The no-locale list ("ANY" in Optimizely's UI) is not an enabled language, so a fan-out
            // never writes into it: that would multiply rules a locale-scoped query cannot see.
            // It only ever receives a direct add while it is the selected language.
            var targets = allLanguages
                ? EnabledLanguages()
                : new List<TargetLanguage> { TargetLanguage.For(lang) };

            var added = new List<string>();
            var skipped = new List<string>();
            var failed = new List<LanguageFailure>();
            var pending = new List<(TargetLanguage Target, IList<SynonymEntry> Current)>();

            // Read every target before writing any of them. Reading as we go would let a write
            // to one language change what a later read sees — Graph's language_routing does not
            // always map one CMS language to one document, so a language can come back already
            // containing the entry this very loop just wrote, and get reported as "already
            // present" when in fact it was added a moment ago.
            foreach (var target in targets)
            {
                try
                {
                    var current = _csv.ParseGraphBody(await _synonymClient.GetRawAsync(resolvedSlot, target.Route));
                    if (current.Any(e => e.RowKey == newEntry.RowKey))
                    {
                        skipped.Add(target.Id);
                        continue;
                    }

                    pending.Add((target, current));
                }
                catch (HttpRequestException ex)
                {
                    failed.Add(new LanguageFailure { Language = target.Id, Error = ex.Message });
                }
            }

            foreach (var (target, current) in pending)
            {
                try
                {
                    current.Add(newEntry);
                    await _synonymClient.PutRawAsync(resolvedSlot, target.Route, _csv.ToGraphBody(current));
                    added.Add(target.Id);
                }
                catch (HttpRequestException ex)
                {
                    failed.Add(new LanguageFailure { Language = target.Id, Error = ex.Message });
                }
            }

            // A single-language add that failed is a plain error; nothing partial happened.
            if (!allLanguages && failed.Count > 0)
            {
                return StatusCode((int)HttpStatusCode.BadGateway, failed[0].Error);
            }
            if (!allLanguages && skipped.Count > 0)
            {
                return Conflict("That synonym already exists in this language and slot.");
            }

            return Ok(new { entry = newEntry, added, skipped, failed });
        }

        public class LanguageFailure
        {
            public string Language { get; set; }
            public string Error { get; set; }
        }

        /// <summary>
        /// One CMS language: the id the editor recognises from the language picker, and the ISO
        /// code Graph actually routes on. These differ — "nn-NO" routes as "nn" — and reporting
        /// the route back to the editor names a language that does not exist in their CMS.
        /// </summary>
        public class TargetLanguage
        {
            public string Id { get; set; }
            public string Route { get; set; }

            public static TargetLanguage For(string languageId)
            {
                return new TargetLanguage
                {
                    Id = languageId,
                    Route = LanguageNormalizer.ToIsoCode(SynonymLanguage.RouteFor(languageId))
                };
            }
        }

        /// <summary>
        /// The enabled CMS languages, one per ISO code Graph routes on. Two CMS languages can
        /// share a code ("en" and "en-GB" both route to "en"), and writing that route twice
        /// would be a wasted read-modify-write over the same document.
        /// </summary>
        private List<TargetLanguage> EnabledLanguages()
        {
            return _resolver.ListLanguages()
                .Select(TargetLanguage.For)
                .Where(t => !string.IsNullOrWhiteSpace(t.Route))
                .GroupBy(t => t.Route, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
        }

        /// <summary>
        /// The row key is the synonym's identity — "b:phrase1,phrase2|synonym" — and is built
        /// from editor-supplied text. It travels as a query parameter rather than a route
        /// segment because a phrase containing a slash encodes to %2F, which IIS rejects by
        /// default and which would turn the delete into a silent 404.
        /// </summary>
        [HttpDelete]
        public async Task<IActionResult> Delete([FromQuery] string rowKey, [FromQuery] string lang, [FromQuery] string slot = null)
        {
            if (string.IsNullOrWhiteSpace(lang))
            {
                return BadRequest("lang query parameter is required.");
            }
            if (string.IsNullOrWhiteSpace(rowKey))
            {
                return BadRequest("rowKey query parameter is required.");
            }

            try
            {
                var resolvedSlot = ResolveSlot(slot);
                var route = SynonymLanguage.RouteFor(lang);
                var current = _csv.ParseGraphBody(await _synonymClient.GetRawAsync(resolvedSlot, route));
                var remaining = current.Where(e => e.RowKey != rowKey).ToList();

                if (remaining.Count == current.Count)
                {
                    return NotFound("No synonym with that row key in this language and slot.");
                }

                await _synonymClient.PutRawAsync(resolvedSlot, route, _csv.ToGraphBody(remaining));
                return NoContent();
            }
            catch (HttpRequestException ex)
            {
                return GraphError(ex);
            }
        }

        /// <summary>
        /// The only endpoint here that accepts multipart/form-data, which browsers send
        /// cross-origin without a CORS preflight. The JSON endpoints are shielded by that
        /// preflight; this one needs the antiforgery token the admin page already renders.
        /// </summary>
        [HttpPost("import")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Import([FromQuery] string lang, [FromQuery] string slot, IFormFile file)
        {
            if (string.IsNullOrWhiteSpace(lang))
            {
                return BadRequest("lang query parameter is required.");
            }
            if (file == null || file.Length == 0)
            {
                return BadRequest("CSV file is required.");
            }

            using var stream = file.OpenReadStream();
            var parsed = _csv.ParseCsv(stream);

            try
            {
                var resolvedSlot = ResolveSlot(slot);
                var route = SynonymLanguage.RouteFor(lang);
                var current = _csv.ParseGraphBody(await _synonymClient.GetRawAsync(resolvedSlot, route));
                var existingKeys = current.Select(e => e.RowKey).ToHashSet();

                var added = 0;
                foreach (var entry in parsed)
                {
                    if (existingKeys.Add(entry.RowKey))
                    {
                        current.Add(entry);
                        added++;
                    }
                }

                await _synonymClient.PutRawAsync(resolvedSlot, route, _csv.ToGraphBody(current));

                // Report both numbers: the editor picked a file with `parsed.Count` rows in it,
                // and saying "imported 40" when 12 were duplicates that changed nothing is a lie.
                return Ok(new { imported = added, skipped = parsed.Count - added, total = parsed.Count });
            }
            catch (HttpRequestException ex)
            {
                return GraphError(ex);
            }
        }

        [HttpGet("export")]
        public async Task<IActionResult> Export([FromQuery] string lang, [FromQuery] string slot = null)
        {
            if (string.IsNullOrWhiteSpace(lang))
            {
                return BadRequest("lang query parameter is required.");
            }

            try
            {
                var resolvedSlot = ResolveSlot(slot);
                var body = await _synonymClient.GetRawAsync(resolvedSlot, SynonymLanguage.RouteFor(lang));
                var entries = _csv.ParseGraphBody(body);
                var csvBytes = Encoding.UTF8.GetBytes(_csv.ToCsv(entries));
                return File(csvBytes, "text/csv", $"synonyms-{lang}-slot-{resolvedSlot}.csv");
            }
            catch (HttpRequestException ex)
            {
                return GraphError(ex);
            }
        }

        /// <summary>
        /// Graph accepts only the two documented slot names. Anything else falls back to the
        /// configured default rather than being passed through to Graph.
        /// </summary>
        private string ResolveSlot(string slot)
        {
            if (string.IsNullOrWhiteSpace(slot))
            {
                return _resolver.DefaultSlot();
            }

            var normalized = slot.Trim().ToLowerInvariant();
            return normalized == "one" || normalized == "two" ? normalized : _resolver.DefaultSlot();
        }

        /// <summary>
        /// Passes a Graph failure back to the UI with Graph's own status code and message
        /// instead of letting it surface as a bare 500.
        /// </summary>
        private IActionResult GraphError(HttpRequestException ex)
        {
            return StatusCode((int)(ex.StatusCode ?? HttpStatusCode.BadGateway), ex.Message);
        }
    }
}
