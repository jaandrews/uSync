using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core;
using Umbraco.Core.Configuration;
using Umbraco.Core.Configuration.UmbracoSettings;
using Umbraco.Core.Logging;
using Umbraco.Core.IO;

namespace Jumoo.uSync.BackOffice {
    [FileSystemProvider("usync")]
    public class uSyncFileSystem : FileSystemWrapper {
        private readonly IContentSection _contentConfig;
        private readonly ILogger _logger;

        public uSyncFileSystem(IFileSystem wrapped)
            : this(wrapped, UmbracoConfig.For.UmbracoSettings().Content, ApplicationContext.Current.ProfilingLogger.Logger)
        { }

        public uSyncFileSystem(IFileSystem wrapped, IContentSection contentConfig, ILogger logger) : base(wrapped) {
            _logger = logger;
            _contentConfig = contentConfig;
        }
    }
}
