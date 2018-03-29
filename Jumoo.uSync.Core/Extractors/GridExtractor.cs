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
    public class GridExtractor : IContentExtractor {
        public string[] PropertyEditorAliases => new[] { "Umbraco.Grid" };

        IGridConfig gridConfig;
        // List<uSyncContentMapping> usyncMappings;

        public GridExtractor() {
            gridConfig = UmbracoConfig.For.GridConfig(
                ApplicationContext.Current.ProfilingLogger.Logger,
                ApplicationContext.Current.ApplicationCache.RuntimeCache,
                new DirectoryInfo(HttpContext.Current.Server.MapPath(SystemDirectories.AppPlugins)),
                new DirectoryInfo(HttpContext.Current.Server.MapPath(SystemDirectories.Config)),
                HttpContext.Current.IsDebuggingEnabled);
        }

        public IEnumerable<string> GetValues(int dateTypeDefinitionId, string editorAlias, string value) {
            LogHelper.Debug<GridExtractor>("Processing Grid");

            var grid = JsonConvert.DeserializeObject<JObject>(value);
            if (grid == null) {
                LogHelper.Warn<GridExtractor>("Failed To Deserialize Grid Content: {0}", () => value);
                return null;
            }

            var sections = GetArray(grid, "sections");
            var results = new List<string>();
            foreach (var section in sections.Cast<JObject>()) {
                var rows = GetArray(section, "rows");
                foreach (var row in rows.Cast<JObject>()) {
                    var areas = GetArray(row, "areas");
                    foreach (var area in areas.Cast<JObject>()) {
                        var controls = GetArray(area, "controls");
                        foreach (var control in controls.Cast<JObject>()) {
                            var mappedVal = ProcessControl(control, editorAlias);
                            if (mappedVal != null) {
                                results.AddRange(mappedVal);
                            }
                        }
                    }
                }
            }
            return results;
        }
        


        private IEnumerable<string> ProcessControl(JObject control, string editorAlias) {
            LogHelper.Debug<GridExtractor>("Processing: {0}", () => control.ToString());
            var extractor = GetEditorExtractor(control.Value<JObject>("editor"));

            if (extractor == null)
                return null;

            var value = control.Value<object>("value");
            
            return extractor.GetValues(0, editorAlias, value.ToString());
        }

        private IContentExtractor GetEditorExtractor(JObject editor) {
            if (editor == null)
                return null;

            var alias = editor.Value<string>("alias");
            var uSyncAlias = string.Format("grid.{0}", alias);

            // var mapping = usyncMappings.SingleOrDefault(x => x.EditorAlias == uSyncAlias);
            var extractor = ContentExtractorFactory.GetExtractor(uSyncAlias);
            if (extractor == null) {
                // get by view name. 
                var config = gridConfig.EditorsConfig.Editors
                    .SingleOrDefault(x => x.Alias == alias);

                if (config != null) {
                    extractor = ContentExtractorFactory.GetByViewName(config.View);
                }
            }

            return extractor;
        }

        private JArray GetArray(JObject obj, string propertyName) {
            JToken token;
            if (obj.TryGetValue(propertyName, out token)) {
                var asArray = token as JArray;
                return asArray ?? new JArray();
            }
            return new JArray();
        }
        private bool IsJson(string val) {
            val = val.Trim();
            return (val.StartsWith("{") && val.EndsWith("}"))
                || (val.StartsWith("[") && val.EndsWith("]"));
        }
    }
}
