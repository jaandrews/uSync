using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jumoo.uSync.BackOffice.Models {
    public class SendRequestFrontEnd {
        public string Folder { get; set; }
        public IEnumerable<string> MediaFolders { get; set; }
        public bool IncludeChildren { get; set; }
    }
}
