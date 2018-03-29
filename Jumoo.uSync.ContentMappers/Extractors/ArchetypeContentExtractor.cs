using Archetype.Models;
using Jumoo.uSync.Core.Extractors;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Services;

namespace Jumoo.uSync.ContentMappers.Extractors {
    public class ArchetypeContentExtractor : IContentExtractor {
        private readonly IDataTypeService _dataTypeService;
        public string[] PropertyEditorAliases => new[] { "Imulus.Archetype" };

        public ArchetypeContentExtractor() {
            _dataTypeService = ApplicationContext.Current.Services.DataTypeService;
        }
        
        public IEnumerable<string> GetValues(int dataTypeDefinitionId, string editorAlias, string value) {
            string archetypeConfig = _dataTypeService.GetPreValuesCollectionByDataTypeId(dataTypeDefinitionId).PreValuesAsDictionary["archetypeConfig"].Value;

            var config = JsonConvert.DeserializeObject<ArchetypePreValue>(archetypeConfig);

            var typedContent = JsonConvert.DeserializeObject<ArchetypeModel>(value);
            var results = new List<string>();
            foreach (ArchetypePreValueFieldset fieldSet in config.Fieldsets) {
                foreach (ArchetypePreValueProperty property in fieldSet.Properties) {
                    IDataTypeDefinition dataType = _dataTypeService.GetDataTypeDefinitionById(property.DataTypeGuid);

                    LogHelper.Debug<LeBlenderContentExtractor>("Archetype Test: {0}", () => dataType.PropertyEditorAlias);
                    var extractor = ContentExtractorFactory.GetExtractor(dataType.PropertyEditorAlias);

                    if (extractor != null) {
                        var properties = typedContent.Fieldsets.AsQueryable()
                                    .SelectMany(fs => fs.Properties)
                                    .Where(p => p.Alias == property.Alias);
                        foreach (var prop in properties) {
                            var result = extractor.GetValues(dataType.Id, editorAlias, prop.Value.ToString());
                            if (result != null) {
                                results.AddRange(result);
                            }
                        }
                    }
                }
            }
            return results;
        }
    }
}
