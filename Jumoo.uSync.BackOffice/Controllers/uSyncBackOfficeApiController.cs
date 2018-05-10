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
using Umbraco.Web;
using Umbraco.Web.WebApi;
using Umbraco.Web.Routing;
using Jumoo.uSync.BackOffice.Licence;
using Jumoo.uSync.BackOffice.Helpers;
using System.Net.Http;
using Jumoo.uSync.BackOffice.Models;
using Newtonsoft.Json.Linq;
using Umbraco.Core.IO;
using System.Xml.Linq;
using System.Net;

namespace Jumoo.uSync.BackOffice.Controllers
{
    [PluginController("uSync")]
    public class uSyncApiController : UmbracoAuthorizedJsonController
    {
        private IFileSystem _fileSystem;
        public uSyncApiController() {
            _fileSystem = FileSystemProviderManager.Current.GetFileSystemProvider<uSyncFileSystem>();
        }

        private static HttpClient client = new HttpClient();
        [HttpPost]
        public async Task<IHttpActionResult> Send(SendRequestBackEnd req) {
            var domain = Request.Headers.GetValues("Origin").FirstOrDefault();
            var uSyncBackOffice = uSyncBackOfficeContext.Instance;
            var node = Umbraco.TypedContent(req.Id);
            string folder;
            if (node == null) {
                var content = Services.ContentService.GetById(req.Id);
                folder = content.Name.ToSafeFileName();
                var parent = Umbraco.TypedContent(content.ParentId);
                while (parent == null && content.ParentId > -1) {
                    content = Services.ContentService.GetById(content.ParentId);
                    folder = content.Name.ToSafeFileName() + "/" + folder;
                    parent = Umbraco.TypedContent(content.ParentId);
                }
                if (parent != null) {
                    folder = parent.Url + folder;
                    folder = parent.AncestorOrSelf(1).UrlName + folder;
                }
                LogHelper.Debug<uSyncApiController>("Getting non published url.");
            }
            else {
                folder = node.Url.Substring(1);
                folder = node.AncestorOrSelf(1).UrlName + "/" + folder;
            }
            LogHelper.Debug<uSyncApiController>("Migrating content from '{0}'", () => folder);
            var data = new SendRequestFrontEnd {
                Folder = folder,
                IncludeChildren = req.IncludeChildren
            };
            var filePath = System.IO.Path.Combine(uSyncBackOfficeContext.Instance.Configuration.Settings.MappedFolder(), "Content", folder, "content.config");
            if (_fileSystem.FileExists(filePath)) {
                var fileStream = _fileSystem.OpenFile(filePath);
                XElement item = XElement.Load(fileStream);
                var media = item.Attribute("media");
                if (media != null && !string.IsNullOrEmpty(media.Value)) {
                    data.MediaFolders = Umbraco.TypedMedia(media.Value.Split(',')).Where(x => x != null).Select(x => {
                        var path = x.UrlName;
                        var namePieces = x.Name.Split('.');
                        if (namePieces.Count() > 1) {
                            var piece = namePieces[namePieces.Count() - 1].ToLower();
                            var lastIndex = path.LastIndexOf(piece);
                            path = path.Substring(0, lastIndex) + "." + piece;
                        }
                        var target = x.Parent;
                        while (target != null) {
                            var targetPath = target.UrlName;
                            namePieces = target.Name.Split('.');
                            if (namePieces.Count() > 1) {
                                var piece = namePieces[namePieces.Count() - 1].ToLower();
                                var lastIndex = path.LastIndexOf(piece);
                                targetPath = targetPath.Substring(0, lastIndex) + "." + piece;
                            }
                            path = targetPath + "/" + path;
                            target = target.Parent;
                        }
                        return path;
                    });
                }
                var result = await client.PostAsJsonAsync<SendRequestFrontEnd>(req.Domain + Url.GetUmbracoApiService<uSyncFrontEndController>("Receive"), data);
                if (result.IsSuccessStatusCode) {
                    var content = await result.Content.ReadAsAsync<IEnumerable<uSyncAction>>();
                    return Ok(content);
                }
                var reason = await result.Content.ReadAsStringAsync();
                try {
                    var error = JObject.Parse(reason);
                    return Content(result.StatusCode, error);
                }
                catch {
                    return Content(result.StatusCode, $"{domain}: {reason}");
                }
            }
            else {
                return Content(HttpStatusCode.NotFound, "Usync hasn't synced this node yet. Please republish the node and try again.");
            }
        }

        [HttpGet]
        public IEnumerable<uSyncAction> Report()
        {
            var actions = uSyncBackOfficeContext.Instance.ImportReport();
            return actions;
        }

        [HttpGet]
        public IEnumerable<uSyncAction> Export(bool deleteAction = false)
        {
            var folder = uSyncBackOfficeContext.Instance.Configuration.Settings.MappedFolder();

            if (System.IO.Directory.Exists(folder))
            {
                // delete the sub foldes (this will leave the uSync.Action file)
                // we try three times, usally its because someone has something open
                // so we can't delete a folder. 
                var attempt = 0;
                var success = false;
                while (attempt < 3 && success == false)
                {
                    success = CleanFolder(folder);
                    attempt++;
                }
            }

            if (deleteAction)
            {
                var action = System.IO.Path.Combine(folder, "uSyncActions.config");
                if (System.IO.File.Exists(action))
                    System.IO.File.Delete(action);
            }


            var actions = uSyncBackOfficeContext.Instance.ExportAll();

            // we write a log - when there have been changes, a zero run doesn't get
            // a file written to disk.
            if (actions.Any(x => x.Change > ChangeType.NoChange))
                uSyncActionLogger.SaveActionLog("Export", actions);

            return actions;
        }

        private bool CleanFolder(string folder)
        {
            try
            {
                foreach (var child in System.IO.Directory.GetDirectories(folder))
                {
                    System.IO.Directory.Delete(child, true);
                }
                return true;
            }
            catch (System.IO.IOException ex)
            {
                Logger.Warn<Events>("Cannot Clean Folder - will try three times: {0}", () => ex.Message);
                return false;
            }
        }

        [HttpGet]
        public IEnumerable<uSyncAction> Import(bool force)
        {
            var actions = uSyncBackOfficeContext.Instance.ImportAll(force: force);

            // we write a log - when there have been changes, a zero run doesn't get
            // a file written to disk.
            if (actions.Any(x => x.Change > ChangeType.NoChange))
                uSyncActionLogger.SaveActionLog("Import", actions);

            return actions;
        }

        [HttpGet]
        public BackOfficeSettings GetSettings()
        {
            string addOnString = "";
            List<BackOfficeTab> addOnTabs = new List<BackOfficeTab>();

            var types = TypeFinder.FindClassesOfType<IuSyncAddOn>();
            foreach (var t in types)
            {
                var typeInstance = Activator.CreateInstance(t) as IuSyncAddOn;
                if (typeInstance != null)
                {
                    LogHelper.Debug<Events>("Loading AddOn Versions: {0}", () => typeInstance.GetVersionInfo());
                    addOnString = string.Format("{0} [{1}]", addOnString, typeInstance.GetVersionInfo());
                }
            }

            var tabTypes = TypeFinder.FindClassesOfType<IuSyncTab>();
            foreach(var t in tabTypes)
            {
                var inst = Activator.CreateInstance(t) as IuSyncTab;
                if (inst != null)
                {
                    addOnTabs.Add(inst.GetTabInfo());
                }
            }

            var l = new GoodwillLicence();

            var settings = new BackOfficeSettings()
            {
                backOfficeVersion = uSyncBackOfficeContext.Instance.Version,
                coreVersion = uSyncCoreContext.Instance.Version,
                addOns = addOnString,
                settings = uSyncBackOfficeContext.Instance.Configuration.Settings,
                licenced = l.IsLicenced(),
                addOnTabs = addOnTabs,
                Handlers = uSyncBackOfficeContext.Instance.Handlers.Select(x => x.Name)
            };

            return settings;
        }

        [HttpGet]
        public bool UpdateSyncMode(string mode)
        {
            var settings = uSyncBackOfficeContext.Instance.Configuration.Settings;

            switch (mode.ToLowerInvariant())
            {
                case "auto":
                    settings.ExportAtStartup = false;
                    settings.ExportOnSave = true;
                    settings.Import = true;
                    break;
                case "target":
                    settings.ExportAtStartup = false;
                    settings.ExportOnSave = false;
                    settings.Import = true;
                    break;
                case "source":
                    settings.ExportAtStartup = false;
                    settings.ExportOnSave = true;
                    settings.Import = false;
                    break;
                case "manual":
                    settings.ExportAtStartup = false;
                    settings.ExportOnSave = false;
                    settings.Import = false;
                    break;
                case "other":
                    return false; 
            }

            uSyncBackOfficeContext.Instance.Configuration.SaveSettings(settings);
            return true; 
        }


        [HttpGet]
        public IEnumerable<uSyncHistory> GetHistory()
        {
            return uSyncActionLogger.GetActionHistory(false);
        }

        [HttpGet]
        public int ClearHistory()
        {
            return uSyncActionLogger.ClearHistory();
        }

        [HttpGet]
        public IEnumerable<SyncAction> GetActions()
        {
            // gets the actions from the uSync Action file....
            var uSyncFolder = uSyncBackOfficeContext.Instance.Configuration.Settings.MappedFolder();
            var Tracker = new Helpers.ActionTracker(uSyncFolder);
            return Tracker.GetAllActions();
        }

        [HttpGet]
        public bool RemoveAction(string name, string type)
        {
            // gets the actions from the uSync Action file....
            var uSyncFolder = uSyncBackOfficeContext.Instance.Configuration.Settings.MappedFolder();
            var Tracker = new Helpers.ActionTracker(uSyncFolder);

            return Tracker.RemoveActions(name, type);
        }
    }

    public class BackOfficeSettings
    {
        public string backOfficeVersion { get; set; }
        public string coreVersion { get; set; }
        public string addOns { get; set; }
        public uSyncBackOfficeSettings settings { get; set; }

        public IEnumerable<string> Handlers { get; set; }

        public bool licenced { get; set; }
        public IEnumerable<BackOfficeTab> addOnTabs { get; set; }
    }

    public class BackOfficeTab
    {
        public string name { get; set; }
        public string template { get; set; }
    }
 
    public interface IuSyncAddOn
    {
        string GetVersionInfo();
    }

    public interface IuSyncTab
    { 
        BackOfficeTab GetTabInfo();
    }
}
