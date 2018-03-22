using Jumoo.uSync.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Web.Editors;
using Umbraco.Web.Mvc;
using Umbraco.Web.WebApi;

using Jumoo.uSync.BackOffice.Licence;
using Jumoo.uSync.BackOffice.Helpers;
using Jumoo.uSync.BackOffice.Models;
using System.Net.Http;
using Newtonsoft.Json.Linq;

namespace Jumoo.uSync.BackOffice.Controllers
{
    [PluginController("uSync")]
    public class uSyncFrontEndController : UmbracoApiController
    {
        [HttpPost]
        public IEnumerable<uSyncAction> Recieve(SendRequestFrontEnd req) {
            var domain = Request.Headers.GetValues("Origin").FirstOrDefault();
            var uSyncBackOffice = uSyncBackOfficeContext.Instance;
            if (req.IncludeChildren) {
                return uSyncBackOffice.ImportAll(req.Folder);
            }
            else {
                return uSyncBackOffice.Import("Default", req.Folder, false);
            }
        }
    }
}
