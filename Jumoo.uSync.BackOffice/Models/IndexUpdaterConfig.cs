using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Jumoo.uSync.BackOffice.Models {
    public class IndexUpdaterConfig {
        [XmlAttribute("Index")]
        public string Index { get; set; }
        [XmlAttribute("Enabled")]
        public bool Enabled { get; set; }
    }
}
