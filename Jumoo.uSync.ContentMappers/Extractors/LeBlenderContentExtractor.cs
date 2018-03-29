using Jumoo.uSync.Core.Extractors;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Services;

namespace Jumoo.uSync.ContentMappers.Extractors {
    public class LeBlenderContentExtractor : IContentExtractor {
        readonly IDataTypeService _dataTypeService;
        public string[] PropertyEditorAliases => new[] { "grid.leBlender" };

        public LeBlenderContentExtractor() {
            _dataTypeService = ApplicationContext.Current.Services.DataTypeService;
        }
        
        public IEnumerable<string> GetValues(int dataTypeDefinitionId, string editorAlias, string value) {
            if (!IsJson(value))
                return null;
            if (!IsJsonArray(value))
                value = $"[{value}]";

            var items = JArray.Parse(value);
            if (items != null) {
                var results = new List<string>();
                foreach (var item in items) {
                    foreach (var val in item.Values()) {
                        var dtdValue = val.Value<string>("dataTypeGuid");
                        var propValue = val.Value<JToken>("value").ToString();
                        Guid dtdGuid;
                        if (Guid.TryParse(dtdValue, out dtdGuid)) {
                            var prop = _dataTypeService.GetDataTypeDefinitionById(dtdGuid);
                            if (prop != null) {
                                var extractor = ContentExtractorFactory.GetExtractor(prop.PropertyEditorAlias);
                                if (extractor != null) {
                                    var result = extractor.GetValues(prop.Id, editorAlias, propValue);
                                    if (result != null) {
                                        results.AddRange(result);
                                    }
                                }

                            }

                        }

                    }
                }
                return results;
            }
            return null;
        }

        private bool IsJson(string val) {
            val = val.Trim();
            return (val.StartsWith("{") && val.EndsWith("}"))
                || (val.StartsWith("[") && val.EndsWith("]"));
        }
        private bool IsJsonArray(string val) {
            val = val.Trim();
            return (val.StartsWith("[") && val.EndsWith("]"));
        }

        internal class uSyncLeBlenderGridModel {
            [JsonProperty("dataTypeGuid")]
            public String DataTypeGuid { get; set; }

            [JsonProperty("editorName")]
            public String Name { get; set; }

            [JsonProperty("editorAlias")]
            public String Alias { get; set; }

            [JsonProperty("value")]
            public object Value { get; set; }
        }
    }
}
