using System.Collections.Generic;

namespace Gulla.Optimizely.Graph.Cms.Ui.Models
{
    public class SynonymEntry
    {
        public IList<string> Phrases { get; set; } = new List<string>();

        public string Synonym { get; set; }

        public bool Bidirectional { get; set; }

        public string RowKey =>
            (Bidirectional ? "b:" : "o:") +
            string.Join(",", Phrases) + "|" + Synonym;
    }
}
