using System.IO;
using System.Linq;
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
        public async Task<IActionResult> List([FromQuery] string site, [FromQuery] string lang, [FromQuery] string slot = null)
        {
            if (string.IsNullOrWhiteSpace(site) || string.IsNullOrWhiteSpace(lang))
            {
                return BadRequest("site and lang query parameters are required.");
            }

            var body = await _synonymClient.GetRawAsync(ResolveSlot(site, slot), lang);
            var entries = _csv.ParseGraphBody(body);
            return Ok(entries);
        }

        public class CreateSynonymRequest
        {
            public string Phrases { get; set; }
            public string Synonym { get; set; }
            public bool Bidirectional { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromQuery] string site, [FromQuery] string lang, [FromQuery] string slot, [FromBody] CreateSynonymRequest body)
        {
            if (string.IsNullOrWhiteSpace(site) || string.IsNullOrWhiteSpace(lang))
            {
                return BadRequest("site and lang query parameters are required.");
            }
            if (body == null || string.IsNullOrWhiteSpace(body.Phrases) || string.IsNullOrWhiteSpace(body.Synonym))
            {
                return BadRequest("Phrases and Synonym are required.");
            }

            var resolvedSlot = ResolveSlot(site, slot);
            var current = _csv.ParseGraphBody(await _synonymClient.GetRawAsync(resolvedSlot, lang));

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

            current.Add(newEntry);
            await _synonymClient.PutRawAsync(resolvedSlot, lang, _csv.ToGraphBody(current));
            return Ok(newEntry);
        }

        [HttpDelete("{rowKey}")]
        public async Task<IActionResult> Delete([FromRoute] string rowKey, [FromQuery] string site, [FromQuery] string lang, [FromQuery] string slot = null)
        {
            if (string.IsNullOrWhiteSpace(site) || string.IsNullOrWhiteSpace(lang))
            {
                return BadRequest("site and lang query parameters are required.");
            }

            var resolvedSlot = ResolveSlot(site, slot);
            var current = _csv.ParseGraphBody(await _synonymClient.GetRawAsync(resolvedSlot, lang));
            var remaining = current.Where(e => e.RowKey != rowKey).ToList();

            await _synonymClient.PutRawAsync(resolvedSlot, lang, _csv.ToGraphBody(remaining));
            return NoContent();
        }

        [HttpPost("import")]
        public async Task<IActionResult> Import([FromQuery] string site, [FromQuery] string lang, [FromQuery] string slot, IFormFile file)
        {
            if (string.IsNullOrWhiteSpace(site) || string.IsNullOrWhiteSpace(lang))
            {
                return BadRequest("site and lang query parameters are required.");
            }
            if (file == null || file.Length == 0)
            {
                return BadRequest("CSV file is required.");
            }

            using var stream = file.OpenReadStream();
            var imported = _csv.ParseCsv(stream);

            var resolvedSlot = ResolveSlot(site, slot);
            var current = _csv.ParseGraphBody(await _synonymClient.GetRawAsync(resolvedSlot, lang));
            var existingKeys = current.Select(e => e.RowKey).ToHashSet();
            foreach (var entry in imported.Where(e => !existingKeys.Contains(e.RowKey)))
            {
                current.Add(entry);
            }

            await _synonymClient.PutRawAsync(resolvedSlot, lang, _csv.ToGraphBody(current));
            return Ok(new { imported = imported.Count });
        }

        [HttpGet("export")]
        public async Task<IActionResult> Export([FromQuery] string site, [FromQuery] string lang, [FromQuery] string slot = null)
        {
            if (string.IsNullOrWhiteSpace(site) || string.IsNullOrWhiteSpace(lang))
            {
                return BadRequest("site and lang query parameters are required.");
            }

            var body = await _synonymClient.GetRawAsync(ResolveSlot(site, slot), lang);
            var entries = _csv.ParseGraphBody(body);
            var csvBytes = Encoding.UTF8.GetBytes(_csv.ToCsv(entries));
            return File(csvBytes, "text/csv", $"synonyms-{site}-{lang}.csv");
        }

        private string ResolveSlot(string site, string slot)
        {
            if (string.IsNullOrWhiteSpace(slot))
            {
                return _resolver.SlotFor(site);
            }
            var normalized = slot.Trim().ToLowerInvariant();
            return normalized == "one" || normalized == "two" ? normalized : _resolver.SlotFor(site);
        }
    }
}
