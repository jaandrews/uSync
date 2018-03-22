using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Jumoo.uSync.BackOffice.Models {
    public class Location {
        [XmlAttribute("Name")]
        public string Name { get; set; }
        [XmlAttribute("Alias")]
        public string Alias { get; set; }
        [XmlAttribute("Url")]
        public string Url { get; set; }
        [XmlAttribute("Enabled")]
        public string Enabled { get; set; }
    }
}
