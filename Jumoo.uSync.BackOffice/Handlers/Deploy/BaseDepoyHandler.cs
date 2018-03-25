﻿using Jumoo.uSync.BackOffice.Helpers;
using Jumoo.uSync.Core;
using Jumoo.uSync.Core.Extensions;
using Jumoo.uSync.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Umbraco.Core.Events;
using Umbraco.Core.Logging;
using Umbraco.Core.Models.EntityBase;
using Umbraco.Core.IO;

namespace Jumoo.uSync.BackOffice.Handlers.Deploy
{
    abstract public class BaseDepoyHandler<TService, TItem> where TItem : IEntity
    {
        internal ISyncSerializer<TItem> _baseSerializer;
        internal IFileSystem _fileSystem = FileSystemProviderManager.Current.GetFileSystemProvider<uSyncFileSystem>();
        internal bool RequiresPostProcessing = false;
        internal bool TwoPassImport = false; 
        public string SyncFolder { get; set; }


        #region Importing 
        public IEnumerable<uSyncAction> ImportAll(string folder, bool force)
        {
            List<uSyncAction> actions = new List<uSyncAction>();

            var items = GetImportItems(folder);
            var tree = MakeTree(items, Guid.Empty);

            Dictionary<XElement, TItem> updates = new Dictionary<XElement, TItem>();
            foreach(var branch in tree)
            {
                actions.AddRange(ImportTree(branch, force, updates));
            }

            foreach(var update in updates)
            {
                ImportSecondPass(update.Value, update.Key);
            }

            return actions; 
        }

        /// <summary>
        /// load the import items from disk
        /// </summary>
        /// <param name="folder"></param>
        /// <returns></returns>
        internal IEnumerable<uSyncDeployNode> GetImportItems(string folder, string extension = "config")
        {
            List<uSyncDeployNode> items = new List<uSyncDeployNode>();

            //var mappedFolder = Umbraco.Core.IO.IOHelper.MapPath(folder);
            
            if (_fileSystem.DirectoryExists(folder))
            {
                foreach(var item in _fileSystem.GetFiles(folder, "*." + extension)) {
                    var fileStream = _fileSystem.OpenFile(item);
                    XElement node = XElement.Load(fileStream);
                    if (node != null)
                    {
                        items.Add(new uSyncDeployNode()
                        {
                            Key = GetKey(node),
                            Master = GetMaster(node),
                            Node = node,
                            Filename = item,
                            IsDelete = node.Name.LocalName == "uSyncArchive"
                        });
                    }
                }
            }
            return items;
        }

        internal IEnumerable<uSyncDeployTreeNode> MakeTree(IEnumerable<uSyncDeployNode> items, Guid masterKey)
        {
            LogHelper.Debug<Events>("Make Tree: {0} {1}", () => items.Count(), () => masterKey);
            List<uSyncDeployTreeNode> branch = new List<uSyncDeployTreeNode>();

            var nodes = items.Where(x => x.Master == masterKey);
            foreach (var node in nodes)
            {
                var leaf = new uSyncDeployTreeNode()
                {
                    Node = node
                };

                if (node.Key != Guid.Empty)
                    leaf.Children.AddRange(MakeTree(items, node.Key));

                branch.Add(leaf);
            }

            return branch;
        }

        private IEnumerable<uSyncAction> ImportTree(uSyncDeployTreeNode tree, bool force, IDictionary<XElement, TItem> updates)
        {
            List<uSyncAction> actions = new List<uSyncAction>();

            SyncAttempt<TItem> result = SyncAttempt<TItem>.Succeed(tree.Node.Key.ToString(), ChangeType.NoChange);

            if (tree.Node.IsDelete)
            {               
                result = SyncAttempt<TItem>.Succeed(tree.Node.Key.ToString(), DeleteItem(tree.Node, force));
                if (result.Change == ChangeType.Delete)
                    LogHelper.Info<Events>("Deleted Item: {0} {1}", () => typeof(TItem).ToString(), () => tree.Node.Key);
            }
            else
            {
                result = Import(tree.Node, force);
            }
            if (result.Success && result.Item != null && TwoPassImport)
            {
                updates.Add(tree.Node.Node, result.Item);
            }
            actions.Add(
                uSyncActionHelper<TItem>.SetAction(result, tree.Node.Filename, RequiresPostProcessing));


            foreach (var branch in tree.Children)
            {
                actions.AddRange(ImportTree(branch, force, updates));
            }

            return actions;
        }

        virtual public SyncAttempt<TItem> Import(uSyncDeployNode node, bool force)
        {
            return Import(node.Node, force);
        }

        abstract public ChangeType DeleteItem(uSyncDeployNode node, bool force);

        private SyncAttempt<TItem> Import(XElement node, bool force)
        {
            var result = _baseSerializer.DeSerialize(node, force);

            if (result.Change > ChangeType.NoChange)
                LogHelper.Info<Events>("Import: {0} {1} {2}", () => result.Name, () => result.Success, () => result.Change);

            return result;
        }

        public IEnumerable<uSyncAction> ProcessPostImport(string filepath, IEnumerable<uSyncAction> actions)
        {
            List<uSyncAction> postActions = new List<uSyncAction>();

            if (actions.Any())
            {
                var items = actions.Where(x => x.ItemType == typeof(TItem));
                foreach(var item in items) {
                    var fileStream = _fileSystem.OpenFile(item.FileName);
                    XElement node = XElement.Load(fileStream);
                    if (node != null)
                    {
                        var attempt = Import(node, false);
                        if (attempt.Success && attempt.Change > ChangeType.NoChange && TwoPassImport) 
                        {
                            ImportSecondPass(attempt.Item, node);
                        }
                    }
                }
            }

            return postActions;            
        }

        virtual public void ImportSecondPass(TItem item, XElement node)
        {
            if (_baseSerializer is ISyncSerializerTwoPass<TItem>)
            {
                LogHelper.Debug<uSyncDeployNode>("Second Pass import: {0}", () => item.Id);
                LogHelper.Debug<uSyncDeployNode>("Second Pass Node: {0}", () => node.Name.LocalName);
                ((ISyncSerializerTwoPass<TItem>)_baseSerializer).DesearlizeSecondPass(item, node);
            }
        }
        #endregion

        #region Reporting
        public IEnumerable<uSyncAction> Report(string folder)
        {
            List<uSyncAction> actions = new List<uSyncAction>();

            var items = GetImportItems(folder);
            foreach(var item in items)
            {
                actions.Add(Report(item));
            }

            return actions; 
        }

        public uSyncAction Report(uSyncDeployNode item)
        {
            if (item.IsDelete)
            {
                return uSyncActionHelper<TItem>.ReportAction(false, item.Key.ToString(), "Delete - will remove if present");
            }
            else
            {
                var update = _baseSerializer.IsUpdate(item.Node);
                var action = uSyncActionHelper<TItem>.ReportAction(update, item.Node.NameFromNode());
                if (action.Change > ChangeType.NoChange)
                    action.Details = ((ISyncChangeDetail)_baseSerializer).GetChanges(item.Node);

                return action;
            }
        }
        #endregion

        #region Export
        abstract public IEnumerable<TItem> GetAllExportItems();

        public IEnumerable<uSyncAction> ExportAll(string folder)
        {
            List<uSyncAction> actions = new List<uSyncAction>();

            string itemFolder = string.Format("{0}/{1}", folder, SyncFolder);

            var items = GetAllExportItems();
            foreach(var item in items)
            {
                if (item != null)
                    actions.Add(ExportToDisk(item, itemFolder));
            }

            return actions; 
        }

        public virtual uSyncAction ExportToDisk(TItem item, string folder)
        {
            return ExportToDisk(item, folder, "config");
        }

        public uSyncAction ExportToDisk(TItem item, string folder, string extension)
        { 
            if (item == null)
                return uSyncAction.Fail(Path.GetFileName(folder), typeof(TItem), "Item not set");

            var filename = GetFileName(item) + "." + extension;

            var attempt = _baseSerializer.Serialize(item);
            if (attempt.Success)
            {
                DeployIOHelper.SaveNode(attempt.Item, folder, filename);
            }
            return uSyncActionHelper<XElement>.SetAction(attempt, filename);
        }


        public virtual string GetFileName(TItem item)
        {
            return item.Key.ToString();
        }

        #endregion

        #region Event Handling

        internal void Service_Saved(TService sender, SaveEventArgs<TItem> e)
        { 
            if (uSyncEvents.Paused)
                return;

            foreach (var item in e.SavedEntities)
            {
                var action = ExportToDisk(item,
                    string.Format("{0}/{1}", uSyncBackOfficeContext.Instance.Configuration.Settings.Folder, SyncFolder));

            }
        }

        internal void Service_Deleted(TService sender, DeleteEventArgs<TItem> e)
        {
            if (uSyncEvents.Paused)
                return;

            foreach(var item in e.DeletedEntities)
            {
                DeployIOHelper.DeleteNode(item.Key,
                    string.Format("{0}/{1}", uSyncBackOfficeContext.Instance.Configuration.Settings.Folder, SyncFolder));
            }
        }

        internal void Service_Moved(TService sender, MoveEventArgs<TItem> e )
        {
            if (uSyncEvents.Paused)
                return;

            foreach (var item in e.MoveInfoCollection)
            {
                var action = ExportToDisk(item.Entity,
                    string.Format("{0}/{1}", uSyncBackOfficeContext.Instance.Configuration.Settings.Folder, SyncFolder));
            }
        }

        #endregion

        virtual internal Guid GetKey(XElement node)
        {
            return node.KeyOrDefault();
        }

        virtual internal Guid GetMaster(XElement node)
        {
            if (node.Element("Info") != null
                && node.Element("Info").Element("Master") != null
                && node.Element("Info").Element("Master").Attribute("Key") != null)
                return node.Element("Info").Element("Master").Attribute("Key").ValueOrDefault(Guid.Empty);

            return Guid.Empty;
        }
    }
}
