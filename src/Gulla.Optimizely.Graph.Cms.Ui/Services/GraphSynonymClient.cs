using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Gulla.Optimizely.Graph.Cms.Ui.Services
{
    public class GraphSynonymClient : IGraphSynonymClient
    {
        private readonly HttpClient _httpClient;

        public GraphSynonymClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> GetRawAsync(string slot, string language)
        {
            var response = await _httpClient.GetAsync(BuildUri(slot, language));
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return string.Empty;
            }

            await EnsureSuccessOrThrowWithBodyAsync(response);
            return await response.Content.ReadAsStringAsync();
        }

        public async Task PutRawAsync(string slot, string language, string body)
        {
            var content = new StringContent(body ?? string.Empty, Encoding.UTF8, "text/plain");
            var response = await _httpClient.PutAsync(BuildUri(slot, language), content);
            await EnsureSuccessOrThrowWithBodyAsync(response);
        }

        /// <summary>
        /// A null or empty language addresses the list Graph keeps for requests WITHOUT
        /// <c>language_routing</c> — the one Optimizely's UI labels "ANY". Leaving the parameter
        /// out is the honest form; sending it empty happens to land in the same list, but so
        /// does any value Graph does not recognise, so the omission is spelled out here.
        /// </summary>
        private static string BuildUri(string slot, string language)
        {
            var route = LanguageNormalizer.ToIsoCode(language);
            var slotParameter = "synonym_slot=" + WebUtility.UrlEncode(slot);
            return string.IsNullOrEmpty(route)
                ? "resources/synonyms?" + slotParameter
                : "resources/synonyms?language_routing=" + WebUtility.UrlEncode(route) + "&" + slotParameter;
        }

        private static async Task EnsureSuccessOrThrowWithBodyAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Optimizely Graph returned {(int)response.StatusCode} {response.ReasonPhrase} for {response.RequestMessage?.RequestUri}. Body: {body}",
                null,
                response.StatusCode);
        }
    }
}
