using System.Collections.Generic;

namespace SolrAdministrationToolKit.Client.Domain
{
    public class CommandButton
    {
        public string Label { get; set; }
        public string ToolTip { get; set; }
        public List<string> IconKinds { get; set; }
    }
}
