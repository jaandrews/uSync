using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jumoo.uSync.Core.Extractors {
    public class MediaPickerExtractor : IContentExtractor {
        public string[] PropertyEditorAliases => new[] { "Umbraco.MediaPicker", "Umbraco.MediaPicker2", "MultipleMediaPicker" };
        public IEnumerable<string> GetValues(int dateTypeDefinitionId, string editorAlias, string value) {
            if (editorAlias == "media" && !string.IsNullOrEmpty(value)) {
                return value.Split(',');
            }
            return null;
        }
    }
}
