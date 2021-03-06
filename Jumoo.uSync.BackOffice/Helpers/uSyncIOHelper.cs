﻿

namespace Jumoo.uSync.BackOffice.Helpers
{
    using System;
    using System.Linq;
    using System.Xml.Linq;
    using System.IO;

    using Umbraco.Core;
    using Umbraco.Core.Logging;
    using Core.Extensions;
    using Umbraco.Core.IO;
    using System.Text.RegularExpressions;

    public class uSyncIOHelper
    {
        public static string SavePath(string root, string type, string filePath)
        {
            var relativePath = Path.Combine(root, type, filePath + ".config");
            var regex = new Regex("^~?\\/");
            return regex.Replace(relativePath, "");
        }

        public static string SavePath(string root, string type, string path, string name)
        {
            var relativePath = Path.Combine(root, type, path, name + ".config");
            var regex = new Regex("^~?\\/");
            return regex.Replace(relativePath, "");
        }

        public static void SaveNode(XElement node, string path)
        {
            try
            {
                
                var fs = FileSystemProviderManager.Current.GetFileSystemProvider<uSyncFileSystem>();
                if (fs.FileExists(path)) {
                    LogHelper.Warn<uSyncEvents>("Archiving: ", () => path);
                    ArchiveFile(path);

                    // remove
                    // File.Delete(path);
                }

                //string folder = Path.GetDirectoryName(path);
                //if (!Directory.Exists(folder))
                //    Directory.CreateDirectory(folder);
                LogHelper.Debug<uSyncIOHelper>("Saving XML to Disk: {0}", () => path);

                uSyncEvents.fireSaving(new uSyncEventArgs { fileName = path });
                fs.AddFile(path, GetStream(node));
                //node.Save(path);

                uSyncEvents.fireSaved(new uSyncEventArgs { fileName = path });
            }
            catch(Exception ex)
            {
                LogHelper.Warn<uSyncEvents>($"Failed to save node: {ex.ToString()}", () => ex.ToString());
            }
        }

        public static void ArchiveFile(string path)
        {
            LogHelper.Debug<uSyncIOHelper>("Archive: {0}", () => path);
            try {
                var fs = FileSystemProviderManager.Current.GetFileSystemProvider<uSyncFileSystem>();
                if (!uSyncBackOfficeContext.Instance.Configuration.Settings.ArchiveVersions)
                {
                    DeleteFile(path);
                    return;
                }

                string fileName = Path.GetFileNameWithoutExtension(path);
                string folder = Path.GetDirectoryName(path);

                var root = uSyncBackOfficeContext.Instance.Configuration.Settings.MappedFolder();
                var regex = new Regex("^~?\\/");
                var archiveRoot = regex.Replace(uSyncBackOfficeContext.Instance.Configuration.Settings.ArchiveFolder, "");
                string filePath = path.Substring(root.Length);

                var archiveFile = string.Format("{0}\\{1}\\{2}_{3}.config",
                                        archiveRoot,
                                        filePath, fileName.ToSafeFileName(),
                                        DateTime.Now.ToString("ddMMyy_HHmmss"));

                //if (!Directory.Exists(Path.GetDirectoryName(archiveFile)))
                //    Directory.CreateDirectory(Path.GetDirectoryName(archiveFile));

                if (fs.FileExists(path))
                {
                    if (fs.FileExists(archiveFile)) {
                        fs.DeleteFile(archiveFile);
                    }
                    fs.CopyFile(path, archiveFile);

                    // archive does delete. because it is always called before a save, 
                    // calling archive without a save is just like deleting (but saving)
                    DeleteFile(path);
                }
            }
            catch(Exception ex)
            {
                LogHelper.Warn<uSyncEvents>("Failed to Archive the existing file (might be locked?) {0}", () => ex.ToString());
            }
        }

        public static void ArchiveRelativeFile(string type, string path, string name)
        {
            string fullpath = Path.Combine(type, path, name);
            ArchiveRelativeFile(fullpath);
        }

        public static void ArchiveRelativeFile(string path, string name)
        {
            string fullPath = Path.Combine(path, name);
            ArchiveRelativeFile(fullPath);
        }

        public static void ArchiveRelativeFile(string fullPath) {
            var regex = new Regex("^~?\\/");
            var uSyncFolder = regex.Replace(uSyncBackOfficeContext.Instance.Configuration.Settings.Folder, "");
            var fullFolder = Path.Combine(uSyncFolder, fullPath + ".config");

            ArchiveFile(fullFolder);
        }

        internal static void DeleteFile(string file)
        {
            LogHelper.Debug<uSyncIOHelper>("Delete File: {0}", () => file);

            uSyncEvents.fireDeleting(new uSyncEventArgs { fileName = file });
            var blankOnDelete = uSyncBackOfficeContext.Instance.Configuration.Settings.PreserveAllFiles;
            var fs = FileSystemProviderManager.Current.GetFileSystemProvider<uSyncFileSystem>();
            if (fs.FileExists(file))
            {
                if (!blankOnDelete)
                {
                    LogHelper.Debug<uSyncIOHelper>("Delete: {0}", () => file);
                    fs.DeleteFile(file);
                }
                else
                {
                    LogHelper.Debug<uSyncIOHelper>("Blank: {0}", () => file);
                    CreateBlank(file);
                }
            }
            else
            {
                LogHelper.Debug<uSyncIOHelper>("Cannot find {0} to delete", ()=> file);
            }

            //var dir = Path.GetDirectoryName(file);
            //if (Directory.Exists(dir))
            //{
            //    if (!Directory.EnumerateFileSystemEntries(dir).Any())
            //        Directory.Delete(dir);
            //}

            uSyncEvents.fireDeleted(new uSyncEventArgs { fileName = file });
        }

        internal static Stream GetStream(XElement node) {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(node.ToString());
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        internal static void CreateBlank(string file)
        {
            var key = Guid.NewGuid();
            var name = "default";

            var fs = FileSystemProviderManager.Current.GetFileSystemProvider<uSyncFileSystem>();
            if (fs.FileExists(file))
            {
                try
                {
                    var existing = XElement.Load(file);
                    if (existing != null && !existing.Name.LocalName.InvariantEquals("uSyncArchive"))
                    {
                        key = existing.KeyOrDefault();
                        name = existing.NameFromNode();
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.Debug<uSyncIOHelper>("Unable to load existing xml: {0}", ()=> ex);
                }
            }
            
            XElement a = new XElement("uSyncArchive",
                new XAttribute("Key", key.ToString()),
                new XAttribute("Name", name));
            fs.AddFile(file, GetStream(a));

        }

        private static void ClenseArchiveFolder(string folder)
        {
            var fs = FileSystemProviderManager.Current.GetFileSystemProvider<uSyncFileSystem>();
            if (fs.DirectoryExists(folder))
            {
                int versions = uSyncBackOfficeContext.Instance.Configuration.Settings.MaxArchiveVersionCount;
                //DirectoryInfo dir = new DirectoryInfo(folder);
                //FileInfo[] fileList = dir.GetFiles("*.config");
                //var files = fileList.OrderByDescending(f => f.CreationTime);
                var files = fs.GetFiles(folder).Where(file => file.Contains(".config"));
                foreach (var file in files.Skip(versions))
                {
                    fs.DeleteFile(file);
                }
            }
        }


        public static string GetShortGuidPath(Guid guid)
        {
            string encoded = Convert.ToBase64String(guid.ToByteArray());
            encoded = encoded
              .Replace("/", "_")
              .Replace("+", "-");
            return encoded.Substring(0, 22);
        }

    }
}
