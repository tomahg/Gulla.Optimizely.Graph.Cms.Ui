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

        private static string BuildUri(string slot, string language)
        {
            return $"resources/synonyms?language_routing={WebUtility.UrlEncode(LanguageNormalizer.ToIsoCode(language))}&synonym_slot={WebUtility.UrlEncode(slot)}";
        }

        private static async Task EnsureSuccessOrThrowWithBodyAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Optimizely Graph returned {(int)response.StatusCode} {response.ReasonPhrase} for {response.RequestMessage?.RequestUri}. Body: {body}");
        }
    }
}
