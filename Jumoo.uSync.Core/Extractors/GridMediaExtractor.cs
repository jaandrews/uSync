using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using Umbraco.Core;
using Umbraco.Core.Configuration;
using Umbraco.Core.Configuration.Grid;
using Umbraco.Core.IO;
using Umbraco.Core.Logging;

namespace Jumoo.uSync.Core.Extractors {
    public class GridMediaExtractor : IContentExtractor {
        public string[] PropertyEditorAliases => new[] { "grid.media" };
        public IEnumerable<string> GetValues(int dateTypeDefinitionId, string editorAlias, string value) {
            if (value != null) {
                var id = JObject.Parse(value)["id"];
                if (id == null || id.ToString() == "") {
                    return null;
                }
                return new List<string>() { id.ToString() };
            }
            return null;
        }
    }
}
