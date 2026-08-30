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
                var body = await _synonymClient.GetRawAsync(ResolveSlot(slot), lang);
                return Ok(_csv.ParseGraphBody(body));
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

        [HttpPost]
        public async Task<IActionResult> Create([FromQuery] string lang, [FromQuery] string slot, [FromBody] CreateSynonymRequest body)
        {
            if (string.IsNullOrWhiteSpace(lang))
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

            try
            {
                var resolvedSlot = ResolveSlot(slot);
                var current = _csv.ParseGraphBody(await _synonymClient.GetRawAsync(resolvedSlot, lang));

                if (current.Any(e => e.RowKey == newEntry.RowKey))
                {
                    return Conflict("That synonym already exists in this language and slot.");
                }

                current.Add(newEntry);
                await _synonymClient.PutRawAsync(resolvedSlot, lang, _csv.ToGraphBody(current));
                return Ok(newEntry);
            }
            catch (HttpRequestException ex)
            {
                return GraphError(ex);
            }
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
                var current = _csv.ParseGraphBody(await _synonymClient.GetRawAsync(resolvedSlot, lang));
                var remaining = current.Where(e => e.RowKey != rowKey).ToList();

                if (remaining.Count == current.Count)
                {
                    return NotFound("No synonym with that row key in this language and slot.");
                }

                await _synonymClient.PutRawAsync(resolvedSlot, lang, _csv.ToGraphBody(remaining));
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
                var current = _csv.ParseGraphBody(await _synonymClient.GetRawAsync(resolvedSlot, lang));
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

                await _synonymClient.PutRawAsync(resolvedSlot, lang, _csv.ToGraphBody(current));

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
                var body = await _synonymClient.GetRawAsync(resolvedSlot, lang);
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
