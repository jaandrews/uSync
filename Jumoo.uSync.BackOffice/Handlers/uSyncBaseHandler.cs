﻿namespace Jumoo.uSync.BackOffice.Handlers
{
    using System.Collections.Generic;
    using System.Linq;
    using System.IO;

    using Jumoo.uSync.Core;

    using Jumoo.uSync.BackOffice.Helpers;

    using Umbraco.Core.Logging;
    using Umbraco.Core.Models.EntityBase;
    using System;
    using Umbraco.Core;
    using Umbraco.Core.Models;
    using System.Xml.Linq;
    using Core.Extensions;
    using Umbraco.Core.IO;

    abstract public class uSyncBaseHandler<T>
    {
        bool _useShortName;
        public IFileSystem _fileSystem;

        public uSyncBaseHandler()
        {
            // short Id Setting, means we save with id.config not {{name}}.config
            _useShortName = uSyncBackOfficeContext.Instance.Configuration.Settings.UseShortIdNames;
            _fileSystem = FileSystemProviderManager.Current.GetFileSystemProvider<uSyncFileSystem>();
        }

        // do things that get imported by this handler then require some form of 
        // post import processing, if this is set to true then the items will
        // also be post processed. 
        internal bool RequiresPostProcessing = false;

        abstract public SyncAttempt<T> Import(string filePath, bool force = false);

        public IEnumerable<uSyncAction> ImportAll(string folder, bool force)
        {
            LogHelper.Info<Logging>("Running Import: {0}", () => Path.GetFileName(folder));
            List<uSyncAction> actions = new List<uSyncAction>();

            Dictionary<string, T> updates = new Dictionary<string, T>();

            // for a non-force sync, we use the actions to process deletes.
            // when it's a force, then we delete anything that is in umbraco
            // that isn't in our folder??
            // if (!force)
            //{
            actions.AddRange(ProcessActions());
            //}

            LogHelper.Info<Logging>("ProcessActions");
            actions.AddRange(ImportFolder(folder, force, updates));


            LogHelper.Info<Logging>("Import Folder");
            if (updates.Any())
            {
                foreach (var update in updates)
                {
                    ImportSecondPass(update.Key, update.Value);
                }
            }

            LogHelper.Info<Logging>("Handler Import Complete: {0} Items {1} changes {2} failures",
                () => actions.Count(),
                () => actions.Count(x => x.Change > ChangeType.NoChange),
                () => actions.Count(x => x.Change > ChangeType.Fail));

            return actions; 
        }

        private IEnumerable<uSyncAction> ImportFolder(string folder, bool force, Dictionary<string, T> updates)
        {
            List<uSyncAction> actions = new List<uSyncAction>();

            var fs = FileSystemProviderManager.Current.GetFileSystemProvider<uSyncFileSystem>();

            if (fs.DirectoryExists(folder))
            {
                foreach (string file in fs.GetFiles(folder, "*.config"))
                {
                    var attempt = Import(file, force);
                    if (attempt.Success && attempt.Item != null)
                    {
                        updates.Add(file, attempt.Item);
                    }

                    actions.Add(uSyncActionHelper<T>.SetAction(attempt, file, RequiresPostProcessing));
                }

                foreach (var children in fs.GetDirectories(folder))
                {
                    actions.AddRange(ImportFolder(children, force, updates));
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
                    LogHelper.Info<Logging>("Processing a Delete: {0}", () => action.TypeName);
                    switch (action.Action)
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

        virtual public string GetItemPath(T item)
        {
            return GetEntityPath((IUmbracoEntity)item);
        }

        internal string GetEntityPath(IUmbracoEntity item)
        {
            string path = string.Empty;
            if (item != null)
            {
                if (item.ParentId > 0)
                {
                    var parent = ApplicationContext.Current.Services.EntityService.Get(item.ParentId);
                    if (parent != null)
                    {
                        path = GetEntityPath(parent);
                    }
                }

                path = Path.Combine(path, GetItemFileName(item));
            }
            return path;

        }

        /// <summary>
        ///  second pass placeholder, some things require a second pass
        ///  (doctypes for structures to be in place)
        /// 
        ///  they just override this function to do their thing.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="item"></param>
        virtual public void ImportSecondPass(string file, T item)
        {

        }

        /// <summary>
        ///  reutns a list of actions saying what will happen 
        /// on a import (force = false)
        /// </summary>
        /// <param name="folder"></param>
        /// <returns></returns>
        public IEnumerable<uSyncAction> Report(string folder)
        {
            List<uSyncAction> actions = new List<uSyncAction>();

            string mappedfolder = Umbraco.Core.IO.IOHelper.MapPath(folder);

            if (Directory.Exists(mappedfolder))
            {
                foreach (string file in Directory.GetFiles(mappedfolder, "*.config"))
                {
                    actions.Add(ReportItem(file));

                }

                foreach (var children in Directory.GetDirectories(mappedfolder))
                {
                    actions.AddRange(Report(children));
                }
            }

            return actions;
        }

        abstract public uSyncAction ReportItem(string file);

        protected string GetItemFileName(IUmbracoEntity item)
        {
            if (item != null)
            {
                if (_useShortName)
                    return uSyncIOHelper.GetShortGuidPath(item.Key);

                return item.Name.ToSafeFileName();
            }

            // we should never really get here, but if for
            // some reason we do - just return a guid.
            return uSyncIOHelper.GetShortGuidPath(Guid.NewGuid());

        }

        protected string GetItemFileName(IEntity item, string name)
        {
            if (_useShortName)
                return uSyncIOHelper.GetShortGuidPath(item.Key);

            return name.ToSafeFileName();
        }
    }
}
