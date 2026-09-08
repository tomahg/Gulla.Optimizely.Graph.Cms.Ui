using System.Threading.Tasks;

namespace Gulla.Optimizely.Graph.Cms.Ui.Services
{
    /// <summary>
    /// Reads and writes one synonym list, addressed by slot and language. The language is the CMS
    /// id ("nb-NO"); the client normalises it to the code Graph routes on. A null language
    /// addresses the no-locale list that Graph keeps for requests without language_routing.
    /// </summary>
    public interface IGraphSynonymClient
    {
        Task<string> GetRawAsync(string slot, string language);

        Task PutRawAsync(string slot, string language, string body);
    }
}
