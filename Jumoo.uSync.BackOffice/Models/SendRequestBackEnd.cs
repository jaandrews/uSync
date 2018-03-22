using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jumoo.uSync.BackOffice.Models {
    public class SendRequestBackEnd {
        [JsonProperty("id")]
        public int Id { get; set; }
        [JsonProperty("domain")]
        public string Domain { get; set; }
        [JsonProperty("includeChildren")]
        public bool IncludeChildren { get; set; }
    }
}
