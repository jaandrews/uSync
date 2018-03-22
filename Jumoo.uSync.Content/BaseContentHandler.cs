﻿using Jumoo.uSync.BackOffice;
using Jumoo.uSync.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Logging;
using Umbraco.Core.Services;
using Jumoo.uSync.BackOffice.Helpers;
using System.Xml.Linq;
using Jumoo.uSync.Core.Extensions;
using Umbraco.Core.IO;
using System.Text.RegularExpressions;

namespace Jumoo.uSync.Content
{
    abstract public class BaseContentHandler<T>
        where T : IContentBase
    {
        internal IContentService _contentService;
        internal IMediaService _mediaService; 

        internal const string mediaFolderName = "_uSyncMedia";
        internal string _exportFileName = "content";

        private bool _ignorePathSettingOn;
        private bool _rootPathSettingOn;
        private bool _levelPathsOn;

        public BaseContentHandler(string fileName)
        {
            _contentService = ApplicationContext.Current.Services.ContentService;
            _mediaService = ApplicationContext.Current.Services.MediaService;
            _exportFileName = fileName;

            _ignorePathSettingOn = false;
            _rootPathSettingOn = false;
            _levelPathsOn = false; 

            // short Id Setting, means we save with id.config not {{name}}.config
            handlerSettings = new BaseContentHandlerSettings();
            handlerSettings.UseShortName = uSyncBackOfficeContext.Instance.Configuration.Settings.UseShortIdNames;
        }

        #region BaseImport

        abstract public SyncAttempt<T> Import(string file, int parentId, bool force = false);
        virtual public SyncAttempt<T> ImportRedirect(string file, bool force = false)
        {
            return SyncAttempt<T>.Succeed(file, ChangeType.NoChange);
        }
        virtual public void ImportSecondPass(string file, T item) {}

        public IEnumerable<uSyncAction> ImportAll(string folder, bool force)
        {
            LogHelper.Debug<Logging>("Running Content Import: {0}", () => Path.GetFileName(folder));

            Dictionary<string, T> updates = new Dictionary<string, T>();

            List<uSyncAction> actions = new List<uSyncAction>();

            string mappedFolder = Umbraco.Core.IO.IOHelper.MapPath(folder);

            actions.AddRange(ProcessActions());

            actions.AddRange(ImportFolder(mappedFolder, -1, force, updates));

            if (updates.Any())
            {
                foreach(var update in updates)
                {
                    ImportSecondPass(update.Key, update.Value);
                }
            }

            return actions;
        }

        private IEnumerable<uSyncAction> ImportFolder(string folder, int parentId, bool force, Dictionary<string, T> updates)
        {
            LogHelper.Debug<ContentHandler>("Import Folder: {0} {1}", () => folder, () => parentId);
            int itemId = parentId;
            List<uSyncAction> actions = new List<uSyncAction>();

            if (Directory.Exists(folder))
            {
                foreach (string file in Directory.GetFiles(folder, string.Format("{0}.config", _exportFileName)))
                {
                    var attempt = Import(file, parentId, force);
                    if (attempt.Success && attempt.Change > ChangeType.NoChange && attempt.Item != null)
                    {
                        updates.Add(file, attempt.Item);
                    }

                    if (attempt.Item != null)
                        itemId = ((IContentBase)attempt.Item).Id;

                    actions.Add(uSyncActionHelper<T>.SetAction(attempt, file));
                }

                // redirects...
                foreach(string file in Directory.GetFiles(folder, "redirect.config"))
                {
                    var attempt = ImportRedirect(file, force);
                    actions.Add(uSyncActionHelper<T>.SetAction(attempt, file));
                }

                foreach (var child in Directory.GetDirectories(folder))
                {
                    if (!Path.GetFileName(child).Equals("_uSyncMedia", StringComparison.OrdinalIgnoreCase))
                    {
                        actions.AddRange(ImportFolder(child, itemId, force, updates));
                    }
                }
            }
            return actions;
        }

        private IEnumerable<uSyncAction> ProcessActions()
        {
            List<uSyncAction> syncActions = new List<uSyncAction>();

            var actions = uSyncBackOfficeContext.Instance.Tracker.GetActions(typeof(T));

            if (actions != null && actions.Any())
            {
                foreach(var action in actions)
                {
                    switch(action.Action)
                    {
                        case SyncActionType.Delete:
                            syncActions.Add(DeleteItem(action.Key, action.Name));
                            break;
                    }
                }
            }

            return syncActions;
        }

        virtual public uSyncAction DeleteItem(Guid key, string keyString)
        {
            return new uSyncAction();
        }

        #endregion

        #region  Base Export


        #endregion

        #region Base Report
        public IEnumerable<uSyncAction> Report(string folder)
        {
            List<uSyncAction> actions = new List<uSyncAction>();

            string mappedFolder = folder; 
            if (folder.StartsWith("~"))
                mappedFolder = Umbraco.Core.IO.IOHelper.MapPath(folder);
            var regex = new Regex("^~?\\/");
            mappedFolder = regex.Replace(mappedFolder, "");
            var fs = FileSystemProviderManager.Current.GetFileSystemProvider<uSyncFileSystem>();
            if (fs.DirectoryExists(mappedFolder))
            {
                
                foreach(var file in fs.GetFiles(mappedFolder, string.Format("{0}.config", _exportFileName)))
                {
                    actions.Add(ReportItem(file));
                }

                foreach(var child in fs.GetDirectories(mappedFolder))
                {
                    actions.AddRange(Report(child));
                }

            }

            return actions;
        }

        abstract public uSyncAction ReportItem(string file);
        #endregion

        #region Base Settings Load

        //
        // implimenting ISyncHanlderConfig for both media and content. 

        private List<uSyncHandlerSetting> _settings;


        protected BaseContentHandlerSettings handlerSettings { get; set; }


        public void LoadHandlerConfig(IEnumerable<uSyncHandlerSetting> settings)
        {
            LogHelper.Debug<ContentHandler>("Loading Handler Settings {0}", () => settings.Count());

            _settings = settings.ToList();

            if (_settings.Any())
            {
                foreach(var setting in _settings.ToList())
                {
                    switch(setting.Key.ToLower())
                    {
                        case "useshortidnames":
                            bool idNameVal = false;
                            if (bool.TryParse(setting.Value, out idNameVal))
                                handlerSettings.UseShortName = idNameVal;
                            break;
                        case "root":
                            handlerSettings.Root = setting.Value;
                            _rootPathSettingOn = !String.IsNullOrEmpty(handlerSettings.Root);
                            LogHelper.Debug<ContentHandler>("Root Setting: {0}", () => handlerSettings.Root);
                            break;
                        case "ignore":
                            handlerSettings.Ignore = setting.Value;
                            _ignorePathSettingOn = !String.IsNullOrEmpty(handlerSettings.Ignore);
                            LogHelper.Debug<ContentHandler>("Ignore Setting: {0}", () => handlerSettings.Ignore);
                            break;
                        case "levelpath":
                            bool.TryParse(setting.Value, out _levelPathsOn);
                            LogHelper.Debug<ContentHandler>("Level Paths : {0}", ()=> _levelPathsOn);
                            break;
                        case "deleteactions":
                            bool delete = false;
                            if (bool.TryParse(setting.Value, out delete))
                                handlerSettings.DeleteActions = delete;
                            break;
                    }
                }
            }
        }


        #endregion


        /// <summary>
        ///  will either return the path (as expected) or a path that
        ///  uses the letters of the key, and the level to make a short
        ///  path..
        /// </summary>
        /// <param name="item"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        protected string GetSavePath(IContentBase item, string path)
        {
            if (!_levelPathsOn)
            {
                return path;
            }
            else
            {
                return item.Level.ToString("000") + "\\" + GetItemFileName(item);
            }
        }

        protected string GetItemFileName(IContentBase item)
        {
            if (item != null)
            {
                if (handlerSettings.UseShortName)
                    return uSyncIOHelper.GetShortGuidPath(item.Key);

                return item.Name.ToSafeFileName();
            }

            // we should never really get here, but if for
            // some reason we do - just return a guid.
            return uSyncIOHelper.GetShortGuidPath(Guid.NewGuid());
        }

        protected bool IncludeItem(string path, IContentBase item)
        {
            if (!_ignorePathSettingOn && !_rootPathSettingOn)
                return true;
            
            var itemPath = Path.Combine(path, item.Name.ToSafeFileName());

            if (_ignorePathSettingOn)
            {
                // if the path starts with the ignore thing, then we don't include it.
                if (!string.IsNullOrEmpty(handlerSettings.Ignore) 
                    && itemPath.StartsWith(handlerSettings.Ignore, StringComparison.InvariantCultureIgnoreCase))
                {
                    LogHelper.Debug<ContentHandler>("Ignoring: {0} {1}", () => itemPath, () => handlerSettings.Ignore);
                    return false;
                }
            }

            if (_rootPathSettingOn)
            {
                // if root is set but the path DOESN'T start with it we don't include it.
                if (!string.IsNullOrEmpty(handlerSettings.Root)
                    && !itemPath.StartsWith(handlerSettings.Root, StringComparison.InvariantCultureIgnoreCase))
                {
                    LogHelper.Debug<ContentHandler>("Not under root: {0} {1}", () => itemPath, () => handlerSettings.Root);
                    return false;
                }
            }

            return true;
        }

        protected class BaseContentHandlerSettings
        {
            public bool UseShortName { get; set; }

            public string Root { get; set; }
            public string Ignore { get; set; }

            public bool DeleteActions { get; set; }
        }
    }

    
}
