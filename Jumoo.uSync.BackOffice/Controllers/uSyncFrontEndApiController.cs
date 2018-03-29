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
using System.Text.RegularExpressions;
using Umbraco.Core.IO;
using System.Net;

namespace Jumoo.uSync.BackOffice.Controllers
{
    [PluginController("uSync")]
    public class uSyncFrontEndController : UmbracoApiController
    {
        [HttpPost]
        public async Task<IHttpActionResult> Receive(SendRequestFrontEnd req) {
            var protocol = new Regex("http(s)?://");
            var allowedLocations = uSyncBackOfficeContext.Instance.Configuration.Settings.Locations.Select(x => protocol.Replace(x.Url, ""));
            var domain = Request.Headers.Host;
            if (!allowedLocations.Contains(domain)) {
                Logger.Warn<Events>($"Request rejected from '{Request.Headers.Host}'");
                return BadRequest("Invalid Request");
            }
            var uSyncBackOffice = uSyncBackOfficeContext.Instance;

            try {
                foreach (var image in req.MediaFolders) {
                    uSyncBackOffice.Import("media", image, false, false);
                }
                return Ok(uSyncBackOffice.Import("content", req.Folder, false, req.IncludeChildren).Distinct());
            } catch (Exception ex) {
                Logger.Warn<Events>(ex.ToString());
                return Content<Exception>(HttpStatusCode.InternalServerError, ex);
            }
        }
    }
}
