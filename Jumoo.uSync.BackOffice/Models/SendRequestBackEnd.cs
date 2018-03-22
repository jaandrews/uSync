using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jumoo.uSync.BackOffice.Models {
    public class SendRequestBackEnd {
        public string Folder { get; set; }
        public string Domain { get; set; }
        public bool IncludeChildren { get; set; }
    }
}
