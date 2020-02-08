/*
 *
 * (c) Copyright Ascensio System Limited 2010-2018
 *
 * This program is freeware. You can redistribute it and/or modify it under the terms of the GNU 
 * General Public License (GPL) version 3 as published by the Free Software Foundation (https://www.gnu.org/copyleft/gpl.html). 
 * In accordance with Section 7(a) of the GNU GPL its Section 15 shall be amended to the effect that 
 * Ascensio System SIA expressly excludes the warranty of non-infringement of any third-party rights.
 *
 * THIS PROGRAM IS DISTRIBUTED WITHOUT ANY WARRANTY; WITHOUT EVEN THE IMPLIED WARRANTY OF MERCHANTABILITY OR
 * FITNESS FOR A PARTICULAR PURPOSE. For more details, see GNU GPL at https://www.gnu.org/copyleft/gpl.html
 *
 * You can contact Ascensio System SIA by email at sales@onlyoffice.com
 *
 * The interactive user interfaces in modified source and object code versions of ONLYOFFICE must display 
 * Appropriate Legal Notices, as required under Section 5 of the GNU GPL version 3.
 *
 * Pursuant to Section 7 § 3(b) of the GNU GPL you must retain the original ONLYOFFICE logo which contains 
 * relevant author attributions when distributing the software. If the display of the logo in its graphic 
 * form is not reasonably feasible for technical reasons, you must include the words "Powered by ONLYOFFICE" 
 * in every copy of the program you distribute. 
 * Pursuant to Section 7 § 3(e) we decline to grant you any rights under trademark law for use of our trademarks.
 *
*/


extern alias ionic;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

using ASC.Common.Security.Authentication;
using ASC.Core.Tenants;
using ASC.Data.Storage;
using ASC.Files.Core;
using ASC.MessagingSystem;
using ASC.Web.Core.Files;
using ASC.Web.Files.Classes;
using ASC.Web.Files.Helpers;
using ASC.Web.Files.Resources;
using ASC.Web.Files.Utils;
using ASC.Web.Studio.Core;

using Microsoft.Extensions.DependencyInjection;

using File = ASC.Files.Core.File;

namespace ASC.Web.Files.Services.WCFService.FileOperations
{
    internal class FileDownloadOperationData : FileOperationData
    {
        public Dictionary<object, string> FilesDownload { get; }
        public Dictionary<string, string> Headers { get; }

        public FileDownloadOperationData(Dictionary<object, string> folders, Dictionary<object, string> files, Tenant tenant, Dictionary<string, string> headers, bool holdResult = true)
            : base(folders.Select(f => f.Key).ToList(), files.Select(f => f.Key).ToList(), tenant, holdResult)
        {
            FilesDownload = files;
            Headers = headers;
        }
    }

    class FileDownloadOperation : FileOperation<FileDownloadOperationData>
    {
        private readonly Dictionary<object, string> files;
        private readonly Dictionary<string, string> headers;

        public override FileOperationType OperationType
        {
            get { return FileOperationType.Download; }
        }


        public FileDownloadOperation(IServiceProvider serviceProvider, FileDownloadOperationData fileDownloadOperationData)
            : base(serviceProvider, fileDownloadOperationData)
        {
            files = fileDownloadOperationData.FilesDownload;
            headers = fileDownloadOperationData.Headers;
        }


        protected override void Do(IServiceScope scope)
        {
            var entriesPathId = GetEntriesPathId(scope);
            if (entriesPathId == null || entriesPathId.Count == 0)
            {
                if (0 < Files.Count)
                    throw new FileNotFoundException(FilesCommonResource.ErrorMassage_FileNotFound);
                throw new DirectoryNotFoundException(FilesCommonResource.ErrorMassage_FolderNotFound);
            }

            var globalStore = scope.ServiceProvider.GetService<GlobalStore>();
            var filesLinkUtility = scope.ServiceProvider.GetService<FilesLinkUtility>();

            ReplaceLongPath(entriesPathId);

            using var stream = CompressToZip(scope, entriesPathId);
            if (stream != null)
            {
                stream.Position = 0;
                const string fileName = FileConstant.DownloadTitle + ".zip";
                var store = globalStore.GetStore();
                store.Save(
                    FileConstant.StorageDomainTmp,
                    string.Format(@"{0}\{1}", ((IAccount)Thread.CurrentPrincipal.Identity).ID, fileName),
                    stream,
                    "application/zip",
                    "attachment; filename=\"" + fileName + "\"");
                Status = string.Format("{0}?{1}=bulk", filesLinkUtility.FileHandlerPath, FilesLinkUtility.Action);
            }
        }

        private ItemNameValueCollection ExecPathFromFile(IServiceScope scope, File file, string path)
        {
            var fileMarker = scope.ServiceProvider.GetService<FileMarker>();
            fileMarker.RemoveMarkAsNew(file);

            var title = file.Title;

            if (files.ContainsKey(file.ID.ToString()))
            {
                var convertToExt = files[file.ID.ToString()];

                if (!string.IsNullOrEmpty(convertToExt))
                {
                    title = FileUtility.ReplaceFileExtension(title, convertToExt);
                }
            }

            var entriesPathId = new ItemNameValueCollection();
            entriesPathId.Add(path + title, file.ID.ToString());

            return entriesPathId;
        }

        private ItemNameValueCollection GetEntriesPathId(IServiceScope scope)
        {
            var fileMarker = scope.ServiceProvider.GetService<FileMarker>();
            var entriesPathId = new ItemNameValueCollection();
            if (0 < Files.Count)
            {
                var files = FileDao.GetFiles(Files.ToArray());
                files = FilesSecurity.FilterRead(files).ToList();
                files.ForEach(file => entriesPathId.Add(ExecPathFromFile(scope, file, string.Empty)));
            }
            if (0 < Folders.Count)
            {
                FilesSecurity.FilterRead(FolderDao.GetFolders(Files.ToArray())).ToList().Cast<FileEntry>().ToList()
                             .ForEach(folder => fileMarker.RemoveMarkAsNew(folder));

                var filesInFolder = GetFilesInFolders(scope, Folders, string.Empty);
                entriesPathId.Add(filesInFolder);
            }
            return entriesPathId;
        }

        private ItemNameValueCollection GetFilesInFolders(IServiceScope scope, IEnumerable<object> folderIds, string path)
        {
            var fileMarker = scope.ServiceProvider.GetService<FileMarker>();

            CancellationToken.ThrowIfCancellationRequested();

            var entriesPathId = new ItemNameValueCollection();
            foreach (var folderId in folderIds)
            {
                CancellationToken.ThrowIfCancellationRequested();

                var folder = FolderDao.GetFolder(folderId);
                if (folder == null || !FilesSecurity.CanRead(folder)) continue;
                var folderPath = path + folder.Title + "/";

                var files = FileDao.GetFiles(folder.ID, null, FilterType.None, false, Guid.Empty, string.Empty, true);
                files = FilesSecurity.FilterRead(files).ToList();
                files.ForEach(file => entriesPathId.Add(ExecPathFromFile(scope, file, folderPath)));

                fileMarker.RemoveMarkAsNew(folder);

                var nestedFolders = FolderDao.GetFolders(folder.ID);
                nestedFolders = FilesSecurity.FilterRead(nestedFolders).ToList();
                if (files.Count == 0 && nestedFolders.Count == 0)
                {
                    entriesPathId.Add(folderPath, string.Empty);
                }

                var filesInFolder = GetFilesInFolders(scope, nestedFolders.ConvertAll(f => f.ID), folderPath);
                entriesPathId.Add(filesInFolder);
            }
            return entriesPathId;
        }

        private Stream CompressToZip(IServiceScope scope, ItemNameValueCollection entriesPathId)
        {
            var setupInfo = scope.ServiceProvider.GetService<SetupInfo>();
            var fileConverter = scope.ServiceProvider.GetService<FileConverter>();
            var filesMessageService = scope.ServiceProvider.GetService<FilesMessageService>();

            var stream = TempStream.Create();
            using (var zip = new ionic::Ionic.Zip.ZipOutputStream(stream, true))
            {
                zip.CompressionLevel = ionic::Ionic.Zlib.CompressionLevel.Level3;
                zip.AlternateEncodingUsage = ionic::Ionic.Zip.ZipOption.AsNecessary;
                zip.AlternateEncoding = Encoding.UTF8;

                foreach (var path in entriesPathId.AllKeys)
                {
                    var counter = 0;
                    foreach (var entryId in entriesPathId[path])
                    {
                        if (CancellationToken.IsCancellationRequested)
                        {
                            zip.Dispose();
                            stream.Dispose();
                            CancellationToken.ThrowIfCancellationRequested();
                        }

                        var newtitle = path;

                        File file = null;
                        var convertToExt = string.Empty;

                        if (!string.IsNullOrEmpty(entryId))
                        {
                            FileDao.InvalidateCache(entryId);
                            file = FileDao.GetFile(entryId);

                            if (file == null)
                            {
                                Error = FilesCommonResource.ErrorMassage_FileNotFound;
                                continue;
                            }

                            if (file.ContentLength > setupInfo.AvailableFileSize)
                            {
                                Error = string.Format(FilesCommonResource.ErrorMassage_FileSizeZip, FileSizeComment.FilesSizeToString(setupInfo.AvailableFileSize));
                                continue;
                            }

                            if (files.ContainsKey(file.ID.ToString()))
                            {
                                convertToExt = files[file.ID.ToString()];
                                if (!string.IsNullOrEmpty(convertToExt))
                                {
                                    newtitle = FileUtility.ReplaceFileExtension(path, convertToExt);
                                }
                            }
                        }

                        if (0 < counter)
                        {
                            var suffix = " (" + counter + ")";

                            if (!string.IsNullOrEmpty(entryId))
                            {
                                newtitle = 0 < newtitle.IndexOf('.') ? newtitle.Insert(newtitle.LastIndexOf('.'), suffix) : newtitle + suffix;
                            }
                            else
                            {
                                break;
                            }
                        }

                        zip.PutNextEntry(newtitle);

                        if (!string.IsNullOrEmpty(entryId) && file != null)
                        {
                            try
                            {
                                if (fileConverter.EnableConvert(file, convertToExt))
                                {
                                    //Take from converter
                                    using (var readStream = fileConverter.Exec(file, convertToExt))
                                    {
                                        readStream.StreamCopyTo(zip);
                                        if (!string.IsNullOrEmpty(convertToExt))
                                        {
                                            filesMessageService.Send(file, headers, MessageAction.FileDownloadedAs, file.Title, convertToExt);
                                        }
                                        else
                                        {
                                            filesMessageService.Send(file, headers, MessageAction.FileDownloaded, file.Title);
                                        }
                                    }
                                }
                                else
                                {
                                    using var readStream = FileDao.GetFileStream(file);
                                    readStream.StreamCopyTo(zip);
                                    filesMessageService.Send(file, headers, MessageAction.FileDownloaded, file.Title);
                                }
                            }
                            catch (Exception ex)
                            {
                                Error = ex.Message;
                                Logger.Error(Error, ex);
                            }
                        }
                        counter++;
                    }

                    ProgressStep();
                }
            }
            return stream;
        }

        private void ReplaceLongPath(ItemNameValueCollection entriesPathId)
        {
            foreach (var path in new List<string>(entriesPathId.AllKeys))
            {
                CancellationToken.ThrowIfCancellationRequested();

                if (200 >= path.Length || 0 >= path.IndexOf('/')) continue;

                var ids = entriesPathId[path];
                entriesPathId.Remove(path);

                var newtitle = "LONG_FOLDER_NAME" + path.Substring(path.LastIndexOf('/'));
                entriesPathId.Add(newtitle, ids);
            }
        }


        class ItemNameValueCollection
        {
            private readonly Dictionary<string, List<string>> dic = new Dictionary<string, List<string>>();


            public IEnumerable<string> AllKeys
            {
                get { return dic.Keys; }
            }

            public IEnumerable<string> this[string name]
            {
                get { return dic[name].ToArray(); }
            }

            public int Count
            {
                get { return dic.Count; }
            }

            public void Add(string name, string value)
            {
                if (!dic.ContainsKey(name))
                {
                    dic.Add(name, new List<string>());
                }
                dic[name].Add(value);
            }

            public void Add(ItemNameValueCollection collection)
            {
                foreach (var key in collection.AllKeys)
                {
                    foreach (var value in collection[key])
                    {
                        Add(key, value);
                    }
                }
            }

            public void Add(string name, IEnumerable<string> values)
            {
                if (!dic.ContainsKey(name))
                {
                    dic.Add(name, new List<string>());
                }
                dic[name].AddRange(values);
            }

            public void Remove(string name)
            {
                dic.Remove(name);
            }
        }
    }
}