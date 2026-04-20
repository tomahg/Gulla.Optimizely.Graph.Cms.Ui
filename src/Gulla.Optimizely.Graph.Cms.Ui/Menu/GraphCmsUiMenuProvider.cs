using System.Collections.Generic;
using EPiServer.Shell.Navigation;
using Gulla.Optimizely.Graph.Cms.Ui.Configuration;

namespace Gulla.Optimizely.Graph.Cms.Ui.Menu
{
    [MenuProvider]
    public class GraphCmsUiMenuProvider : IMenuProvider
    {
        public IEnumerable<MenuItem> GetMenuItems()
        {
            // /global/cms/admin/scheduledjobs/... places the item in the CMS Settings left
            // sidebar under the Data & Sync Management section (alongside Scheduled Jobs,
            // Smooth Rebuild, GraphiQL).
            return
            [
                new UrlMenuItem("Graph Optimization", MenuPaths.Global + "/cms/admin/scheduledjobs/graphcmsui", "/GraphCmsUi")
                {
                    AuthorizationPolicy = GraphCmsUiAuthorizationPolicy.Default,
                    SortIndex = SortIndex.Last + 200
                }
            ];
        }
    }
}
