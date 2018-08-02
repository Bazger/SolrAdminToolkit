using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReindexAutomation.Client.Domain
{
    public class CommandButton
    {
        public string Label { get; set; }
        public string ToolTip { get; set; }
        public List<string> IconKinds { get; set; }
    }
}
