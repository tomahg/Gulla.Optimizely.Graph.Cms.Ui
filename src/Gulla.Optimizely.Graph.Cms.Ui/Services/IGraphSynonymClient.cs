using System.Threading.Tasks;

namespace Gulla.Optimizely.Graph.Cms.Ui.Services
{
    public interface IGraphSynonymClient
    {
        Task<string> GetRawAsync(string slot, string language);

        Task PutRawAsync(string slot, string language, string body);
    }
}
