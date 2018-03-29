using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jumoo.uSync.Core.Extractors {
    public interface IContentExtractor {
        IEnumerable<string> GetValues(int dataTypeDefinitionId, string editorAlias, string value);
        string[] PropertyEditorAliases { get; }
    }
}
