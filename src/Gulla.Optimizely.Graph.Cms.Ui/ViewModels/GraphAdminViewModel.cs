using System.Collections.Generic;

namespace Gulla.Optimizely.Graph.Cms.Ui.ViewModels
{
    public class GraphAdminViewModel
    {
        public IReadOnlyList<SiteOption> Sites { get; set; } = new List<SiteOption>();

        public IReadOnlyList<string> Languages { get; set; } = new List<string>();

        public string SelectedSiteKey { get; set; }

        public string SelectedLanguage { get; set; }

        public string ActiveTab { get; set; } = "best-bets";
    }

    public class SiteOption
    {
        public string Key { get; set; }

        public string Name { get; set; }
    }
}
