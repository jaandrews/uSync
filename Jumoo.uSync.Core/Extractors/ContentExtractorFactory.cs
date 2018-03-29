using System;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Logging;

namespace Jumoo.uSync.Core.Extractors {
    public class ContentExtractorFactory
    {
        public static IContentExtractor GetCustomExtractor(string typeDefinition) {
            Type extractorType = Type.GetType(typeDefinition);
            if (extractorType == null) {
                return null;
            }

            LogHelper.Debug<ContentExtractorFactory>("Custom Extractor: {0}", () => extractorType.ToString());

            return Activator.CreateInstance(extractorType) as IContentExtractor;
        }

        public static IContentExtractor GetExtractor(uSyncContentExtractor extractor)
        {
            LogHelper.Debug<ContentExtractorFactory>("Extracting: {0} {1}", () => extractor.EditorAlias, ()=> extractor.ExtractorType);
            switch (extractor.ExtractorType)
            {
                case ContentMappingType.Media:
                    return new MediaPickerExtractor();
                case ContentMappingType.Custom:
                    return ContentExtractorFactory.GetCustomExtractor(extractor.CustomExtractorType);
                default:
                    return null;
            }
        }

        public static IContentExtractor GetExtractor(string alias) {
            var extractor = uSyncCoreContext.Instance.Configuration.Settings.ContentExtractors
                .SingleOrDefault(x => x.EditorAlias.InvariantEquals(alias));

            if (extractor == null)
            {
                // look for a dynamic mappings
                if (uSyncCoreContext.Instance.Extractors
                    .Any(x => x.Key.InvariantEquals(alias)))
                {
                    var extractorFallback = uSyncCoreContext.Instance.Extractors
                        .FirstOrDefault(x => x.Key.InvariantEquals(alias));

                    LogHelper.Debug<ContentExtractorFactory>("Returning Extractor (dynamic): {0}", () => extractorFallback.Key);

                    return extractorFallback.Value as IContentExtractor;
                }

                return null;
            }
            else
            {
                return GetExtractor(extractor);
            }
        }

        public static IContentExtractor GetByViewName(string view) {
            LogHelper.Debug<ContentExtractorFactory>("Returning Extractor by view: {0}", () => view);
            var extractor = uSyncCoreContext.Instance.Configuration.Settings.ContentExtractors
                    .SingleOrDefault(x => !string.IsNullOrEmpty(x.View) && view.IndexOf(x.View, StringComparison.InvariantCultureIgnoreCase) > -1);

            if (extractor != null)
                return GetExtractor(extractor);

            return null;
        }
    }
}