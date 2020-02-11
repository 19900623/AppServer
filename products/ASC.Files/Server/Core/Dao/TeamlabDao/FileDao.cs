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


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;

using ASC.Core;
using ASC.Core.Common.EF;
using ASC.Core.Common.Settings;
using ASC.Core.Tenants;
using ASC.ElasticSearch;
using ASC.Files.Core.EF;
using ASC.Web.Core.Files;
using ASC.Web.Files.Classes;
using ASC.Web.Files.Core.Search;
using ASC.Web.Files.Resources;
using ASC.Web.Files.Services.DocumentService;
using ASC.Web.Files.Utils;
using ASC.Web.Studio.Core;
using ASC.Web.Studio.UserControls.Statistics;
using ASC.Web.Studio.Utility;

using Microsoft.Extensions.DependencyInjection;

namespace ASC.Files.Core.Data
{
    public class FileDao : AbstractDao, IFileDao
    {
        private static readonly object syncRoot = new object();
        public FactoryIndexer<FilesWrapper> FactoryIndexer { get; }
        public GlobalStore GlobalStore { get; }
        public GlobalSpace GlobalSpace { get; }
        public GlobalFolder GlobalFolder { get; }
        public IFolderDao FolderDao { get; }
        public ChunkedUploadSessionHolder ChunkedUploadSessionHolder { get; }

        public FileDao(
            FactoryIndexer<FilesWrapper> factoryIndexer,
            UserManager userManager,
            DbContextManager<FilesDbContext> dbContextManager,
            TenantManager tenantManager,
            TenantUtil tenantUtil,
            SetupInfo setupInfo,
            TenantExtra tenantExtra,
            TenantStatisticsProvider tenantStatisticProvider,
            CoreBaseSettings coreBaseSettings,
            CoreConfiguration coreConfiguration,
            SettingsManager settingsManager,
            AuthContext authContext,
            IServiceProvider serviceProvider,
            GlobalStore globalStore,
            GlobalSpace globalSpace,
            GlobalFolder globalFolder,
            IFolderDao folderDao,
            ChunkedUploadSessionHolder chunkedUploadSessionHolder)
            : base(
                  dbContextManager,
                  userManager,
                  tenantManager,
                  tenantUtil,
                  setupInfo,
                  tenantExtra,
                  tenantStatisticProvider,
                  coreBaseSettings,
                  coreConfiguration,
                  settingsManager,
                  authContext,
                  serviceProvider)
        {
            FactoryIndexer = factoryIndexer;
            GlobalStore = globalStore;
            GlobalSpace = globalSpace;
            GlobalFolder = globalFolder;
            FolderDao = folderDao;
            ChunkedUploadSessionHolder = chunkedUploadSessionHolder;
        }

        public void InvalidateCache(object fileId)
        {
        }

        public File GetFile(object fileId)
        {
            var query = GetFileQuery(r => r.Id.ToString() == fileId.ToString() && r.CurrentVersion);
            return FromQuery(query).SingleOrDefault();
        }

        public File GetFile(object fileId, int fileVersion)
        {
            var query = GetFileQuery(r => r.Id.ToString() == fileId.ToString() && r.Version == fileVersion);
            return FromQuery(query).SingleOrDefault();
        }

        public File GetFile(object parentId, string title)
        {
            if (string.IsNullOrEmpty(title)) throw new ArgumentNullException(title);

            var query = GetFileQuery(r => r.Title == title && r.CurrentVersion == true && r.FolderId.ToString() == parentId.ToString())
                .OrderBy(r => r.CreateOn);

            return FromQuery(query).FirstOrDefault();
        }

        public File GetFileStable(object fileId, int fileVersion = -1)
        {
            var query = GetFileQuery(r => r.Id.ToString() == fileId.ToString() && r.Forcesave == ForcesaveType.None);

            if (fileVersion >= 0)
            {
                query = query.Where(r => r.Version <= fileVersion);
            }

            query = query.OrderByDescending(r => r.Version);

            return FromQuery(query).SingleOrDefault();
        }

        public List<File> GetFileHistory(object fileId)
        {
            var query = GetFileQuery(r => r.Id.ToString() == fileId.ToString()).OrderByDescending(r => r.Version);

            return FromQuery(query);
        }

        public List<File> GetFiles(object[] fileIds)
        {
            if (fileIds == null || fileIds.Length == 0) return new List<File>();

            var query = GetFileQuery(r => fileIds.Any(a => a.ToString() == r.Id.ToString()) && r.CurrentVersion);

            return FromQuery(query);
        }

        public List<File> GetFilesForShare(object[] fileIds, FilterType filterType, bool subjectGroup, Guid subjectID, string searchText, bool searchInContent)
        {
            if (fileIds == null || fileIds.Length == 0 || filterType == FilterType.FoldersOnly) return new List<File>();

            var query = GetFileQuery(r => fileIds.Any(a => a.ToString() == r.Id.ToString()) && r.CurrentVersion);

            if (!string.IsNullOrEmpty(searchText))
            {
                var func = GetFuncForSearch(null, null, filterType, subjectGroup, subjectID, searchText, searchInContent, false);

                if (FactoryIndexer.TrySelectIds(s => func(s).In(r => r.Id, fileIds), out var searchIds))
                {
                    query = query.Where(r => searchIds.Any(b => b == r.Id));
                }
                else
                {
                    query = query.Where(r => BuildSearch(r, searchText, SearhTypeEnum.Any));
                }
            }

            if (subjectID != Guid.Empty)
            {
                if (subjectGroup)
                {
                    var users = UserManager.GetUsersByGroup(subjectID).Select(u => u.ID).ToArray();
                    query = query.Where(r => users.Any(b => b == r.CreateBy));
                }
                else
                {
                    query = query.Where(r => r.CreateBy == subjectID);
                }
            }

            switch (filterType)
            {
                case FilterType.DocumentsOnly:
                case FilterType.ImagesOnly:
                case FilterType.PresentationsOnly:
                case FilterType.SpreadsheetsOnly:
                case FilterType.ArchiveOnly:
                case FilterType.MediaOnly:
                    query = query.Where(r => r.Category == (int)filterType);
                    break;
                case FilterType.ByExtension:
                    if (!string.IsNullOrEmpty(searchText))
                    {
                        query = query.Where(r => BuildSearch(r, searchText, SearhTypeEnum.End));
                    }
                    break;
            }

            return FromQuery(query, false);
        }

        public List<object> GetFiles(object parentId)
        {
            var query = GetFileQuery(r => r.FolderId.ToString() == parentId.ToString() && r.CurrentVersion).Select(r => r.Id);

            return Query(r => r.Files)
                .Where(r => (object)r.FolderId == parentId && r.CurrentVersion)
                .Select(r => (object)r.Id)
                .ToList();
        }

        public List<File> GetFiles(object parentId, OrderBy orderBy, FilterType filterType, bool subjectGroup, Guid subjectID, string searchText, bool searchInContent, bool withSubfolders = false)
        {
            if (filterType == FilterType.FoldersOnly) return new List<File>();

            if (orderBy == null) orderBy = new OrderBy(SortedByType.DateAndTime, false);

            var q = GetFileQuery(r => r.FolderId.ToString() == parentId.ToString() && r.CurrentVersion);


            if (withSubfolders)
            {
                q = GetFileQuery(r => r.CurrentVersion)
                    .Join(FilesDbContext.Tree, r => r.FolderId, a => a.FolderId, (file, tree) => new { file, tree })
                    .Where(r => r.tree.ParentId.ToString() == parentId.ToString())
                    .Select(r => r.file);
            }

            if (!string.IsNullOrEmpty(searchText))
            {

                var func = GetFuncForSearch(parentId, orderBy, filterType, subjectGroup, subjectID, searchText, searchInContent, withSubfolders);

                Expression<Func<Selector<FilesWrapper>, Selector<FilesWrapper>>> expression = s => func(s);

                if (FactoryIndexer.TrySelectIds(expression, out var searchIds))
                {
                    q = q.Where(r => searchIds.Any(a => a == r.Id));
                }
                else
                {
                    q = q.Where(r => BuildSearch(r, searchText, SearhTypeEnum.Any));
                }
            }

            switch (orderBy.SortedBy)
            {
                case SortedByType.Author:
                    q = orderBy.IsAsc ? q.OrderBy(r => r.CreateBy) : q.OrderByDescending(r => r.CreateBy);
                    break;
                case SortedByType.Size:
                    q = orderBy.IsAsc ? q.OrderBy(r => r.ContentLength) : q.OrderByDescending(r => r.ContentLength);
                    break;
                case SortedByType.AZ:
                    q = orderBy.IsAsc ? q.OrderBy(r => r.Title) : q.OrderByDescending(r => r.Title);
                    break;
                case SortedByType.DateAndTime:
                    q = orderBy.IsAsc ? q.OrderBy(r => r.ModifiedOn) : q.OrderByDescending(r => r.ModifiedOn);
                    break;
                case SortedByType.DateAndTimeCreation:
                    q = orderBy.IsAsc ? q.OrderBy(r => r.CreateOn) : q.OrderByDescending(r => r.CreateOn);
                    break;
                default:
                    q = q.OrderBy(r => r.Title);
                    break;
            }

            if (subjectID != Guid.Empty)
            {
                if (subjectGroup)
                {
                    var users = UserManager.GetUsersByGroup(subjectID).Select(u => u.ID).ToArray();
                    q = q.Where(r => users.Any(a => a == r.CreateBy));
                }
                else
                {
                    q = q.Where(r => r.CreateBy == subjectID);
                }
            }

            switch (filterType)
            {
                case FilterType.DocumentsOnly:
                case FilterType.ImagesOnly:
                case FilterType.PresentationsOnly:
                case FilterType.SpreadsheetsOnly:
                case FilterType.ArchiveOnly:
                case FilterType.MediaOnly:
                    q = q.Where(r => r.Category == (int)filterType);
                    break;
                case FilterType.ByExtension:
                    if (!string.IsNullOrEmpty(searchText))
                    {
                        q = q.Where(r => BuildSearch(r, searchText, SearhTypeEnum.End));
                    }
                    break;
            }

            return FromQuery(q);
        }

        public Stream GetFileStream(File file, long offset)
        {
            return GlobalStore.GetStore().GetReadStream(string.Empty, GetUniqFilePath(file), (int)offset);
        }

        public Uri GetPreSignedUri(File file, TimeSpan expires)
        {
            return GlobalStore.GetStore().GetPreSignedUri(string.Empty, GetUniqFilePath(file), expires,
                                                     new List<string>
                                                         {
                                                             string.Concat("Content-Disposition:", ContentDispositionUtil.GetHeaderValue(file.Title, withoutBase: true))
                                                         });
        }

        public bool IsSupportedPreSignedUri(File file)
        {
            return GlobalStore.GetStore().IsSupportedPreSignedUri;
        }

        public Stream GetFileStream(File file)
        {
            return GetFileStream(file, 0);
        }

        public File SaveFile(File file, Stream fileStream)
        {
            if (file == null)
            {
                throw new ArgumentNullException("file");
            }

            var maxChunkedUploadSize = SetupInfo.MaxChunkedUploadSize(TenantExtra, TenantStatisticProvider);
            if (maxChunkedUploadSize < file.ContentLength)
            {
                throw FileSizeComment.GetFileSizeException(maxChunkedUploadSize);
            }

            if (CoreBaseSettings.Personal && SetupInfo.IsVisibleSettings("PersonalMaxSpace"))
            {
                var personalMaxSpace = CoreConfiguration.PersonalMaxSpace(SettingsManager);
                if (personalMaxSpace - GlobalSpace.GetUserUsedSpace(file.ID == null ? AuthContext.CurrentAccount.ID : file.CreateBy) < file.ContentLength)
                {
                    throw FileSizeComment.GetPersonalFreeSpaceException(personalMaxSpace);
                }
            }

            var isNew = false;
            List<object> parentFoldersIds;
            lock (syncRoot)
            {
                using var tx = FilesDbContext.Database.BeginTransaction();

                if (file.ID == null)
                {
                    file.ID = FilesDbContext.Files.Max(r => r.Id) + 1;
                    file.Version = 1;
                    file.VersionGroup = 1;
                    isNew = true;
                }

                file.Title = Global.ReplaceInvalidCharsAndTruncate(file.Title);
                //make lowerCase
                file.Title = FileUtility.ReplaceFileExtension(file.Title, FileUtility.GetFileExtension(file.Title));

                file.ModifiedBy = AuthContext.CurrentAccount.ID;
                file.ModifiedOn = TenantUtil.DateTimeNow();
                if (file.CreateBy == default) file.CreateBy = AuthContext.CurrentAccount.ID;
                if (file.CreateOn == default) file.CreateOn = TenantUtil.DateTimeNow();

                var toUpdate = FilesDbContext.Files
                    .Where(r => (object)r.Id == file.ID && r.CurrentVersion && r.TenantId == TenantID)
                    .FirstOrDefault();

                toUpdate.CurrentVersion = false;
                FilesDbContext.SaveChanges();


                var toInsert = new DbFile
                {
                    Id = (int)file.ID,
                    Version = file.Version,
                    VersionGroup = file.VersionGroup,
                    CurrentVersion = true,
                    FolderId = (int)file.FolderID,
                    Title = file.Title,
                    ContentLength = file.ContentLength,
                    Category = (int)file.FilterType,
                    CreateBy = file.CreateBy,
                    CreateOn = TenantUtil.DateTimeToUtc(file.CreateOn),
                    ModifiedBy = file.ModifiedBy,
                    ModifiedOn = TenantUtil.DateTimeToUtc(file.ModifiedOn),
                    ConvertedType = file.ConvertedType,
                    Comment = file.Comment,
                    Encrypted = file.Encrypted,
                    Forcesave = file.Forcesave,
                    TenantId = TenantID
                };

                FilesDbContext.Files.Add(toInsert);
                FilesDbContext.SaveChanges();

                tx.Commit();

                file.PureTitle = file.Title;

                parentFoldersIds =
                    FilesDbContext.Tree
                    .Where(r => r.FolderId == (int)file.FolderID)
                    .OrderByDescending(r => r.Level)
                    .Select(r => (object)r.ParentId)
                    .ToList();

                if (parentFoldersIds.Count > 0)
                {
                    var folderToUpdate = FilesDbContext.Folders
                        .Where(r => parentFoldersIds.Any(a => a == (object)r.Id));

                    foreach (var f in folderToUpdate)
                    {
                        f.ModifiedOn = TenantUtil.DateTimeToUtc(file.ModifiedOn);
                        f.ModifiedBy = file.ModifiedBy;
                    }

                    FilesDbContext.SaveChanges();
                }

                if (isNew)
                {
                    RecalculateFilesCount(file.FolderID);
                }
            }

            if (fileStream != null)
            {
                try
                {
                    SaveFileStream(file, fileStream);
                }
                catch
                {
                    if (isNew)
                    {
                        var stored = GlobalStore.GetStore().IsDirectory(GetUniqFileDirectory(file.ID));
                        DeleteFile(file.ID, stored);
                    }
                    else if (!IsExistOnStorage(file))
                    {
                        DeleteVersion(file);
                    }
                    throw;
                }
            }

            FactoryIndexer.IndexAsync(FilesWrapper.GetFilesWrapper(ServiceProvider, file, parentFoldersIds));

            return GetFile(file.ID);
        }

        public File ReplaceFileVersion(File file, Stream fileStream)
        {
            if (file == null) throw new ArgumentNullException("file");
            if (file.ID == null) throw new ArgumentException("No file id or folder id toFolderId determine provider");

            var maxChunkedUploadSize = SetupInfo.MaxChunkedUploadSize(TenantExtra, TenantStatisticProvider);

            if (maxChunkedUploadSize < file.ContentLength)
            {
                throw FileSizeComment.GetFileSizeException(maxChunkedUploadSize);
            }

            if (CoreBaseSettings.Personal && SetupInfo.IsVisibleSettings("PersonalMaxSpace"))
            {
                var personalMaxSpace = CoreConfiguration.PersonalMaxSpace(SettingsManager);
                if (personalMaxSpace - GlobalSpace.GetUserUsedSpace(file.ID == null ? AuthContext.CurrentAccount.ID : file.CreateBy) < file.ContentLength)
                {
                    throw FileSizeComment.GetPersonalFreeSpaceException(personalMaxSpace);
                }
            }

            List<object> parentFoldersIds;
            lock (syncRoot)
            {
                using var tx = FilesDbContext.Database.BeginTransaction();

                file.Title = Global.ReplaceInvalidCharsAndTruncate(file.Title);
                //make lowerCase
                file.Title = FileUtility.ReplaceFileExtension(file.Title, FileUtility.GetFileExtension(file.Title));

                file.ModifiedBy = AuthContext.CurrentAccount.ID;
                file.ModifiedOn = TenantUtil.DateTimeNow();
                if (file.CreateBy == default) file.CreateBy = AuthContext.CurrentAccount.ID;
                if (file.CreateOn == default) file.CreateOn = TenantUtil.DateTimeNow();

                var toUpdate = FilesDbContext.Files
                    .Where(r => (object)r.Id == file.ID && r.Version == file.Version)
                    .FirstOrDefault();

                toUpdate.Version = file.Version;
                toUpdate.VersionGroup = file.VersionGroup;
                toUpdate.FolderId = (int)file.FolderID;
                toUpdate.Title = file.Title;
                toUpdate.ContentLength = file.ContentLength;
                toUpdate.Category = (int)file.FilterType;
                toUpdate.CreateBy = file.CreateBy;
                toUpdate.CreateOn = TenantUtil.DateTimeToUtc(file.CreateOn);
                toUpdate.ModifiedBy = file.ModifiedBy;
                toUpdate.ModifiedOn = TenantUtil.DateTimeToUtc(file.ModifiedOn);
                toUpdate.ConvertedType = file.ConvertedType;
                toUpdate.Comment = file.Comment;
                toUpdate.Encrypted = file.Encrypted;
                toUpdate.Forcesave = file.Forcesave;

                FilesDbContext.SaveChanges();

                tx.Commit();

                file.PureTitle = file.Title;

                parentFoldersIds = FilesDbContext.Tree
                    .Where(r => r.FolderId == (int)file.FolderID)
                    .OrderByDescending(r => r.Level)
                    .Select(r => (object)r.ParentId)
                    .ToList();

                if (parentFoldersIds.Count > 0)
                {
                    var folderToUpdate = FilesDbContext.Folders
                        .Where(r => parentFoldersIds.Any(a => a == (object)r.Id));

                    foreach (var f in folderToUpdate)
                    {
                        f.ModifiedOn = TenantUtil.DateTimeToUtc(file.ModifiedOn);
                        f.ModifiedBy = file.ModifiedBy;
                    }

                    FilesDbContext.SaveChanges();
                }
            }

            if (fileStream != null)
            {
                try
                {
                    DeleteVersionStream(file);
                    SaveFileStream(file, fileStream);
                }
                catch
                {
                    if (!IsExistOnStorage(file))
                    {
                        DeleteVersion(file);
                    }
                    throw;
                }
            }

            FactoryIndexer.IndexAsync(FilesWrapper.GetFilesWrapper(ServiceProvider, file, parentFoldersIds));

            return GetFile(file.ID);
        }

        private void DeleteVersion(File file)
        {
            if (file == null
                || file.ID == null
                || file.Version <= 1) return;

            var toDelete = Query(r => r.Files)
                .Where(r => r.Id == (int)file.ID)
                .Where(r => r.Version == file.Version)
                .FirstOrDefault();

            if (toDelete != null)
            {
                FilesDbContext.Files.Remove(toDelete);
            }
            FilesDbContext.SaveChanges();

            var toUpdate = Query(r => r.Files)
                .Where(r => r.Id == (int)file.ID)
                .Where(r => r.Version == file.Version - 1)
                .FirstOrDefault();

            toUpdate.CurrentVersion = true;
            FilesDbContext.SaveChanges();
        }

        private void DeleteVersionStream(File file)
        {
            GlobalStore.GetStore().DeleteDirectory(GetUniqFileVersionPath(file.ID, file.Version));
        }

        private void SaveFileStream(File file, Stream stream)
        {
            GlobalStore.GetStore().Save(string.Empty, GetUniqFilePath(file), stream, file.Title);
        }

        public void DeleteFile(object fileId)
        {
            DeleteFile(fileId, true);
        }

        private void DeleteFile(object fileId, bool deleteFolder)
        {
            if (fileId == null) return;
            using var tx = FilesDbContext.Database.BeginTransaction();

            var fromFolders = Query(r => r.Files).Where(r => r.Id == (int)fileId).GroupBy(r => r.Id).SelectMany(r => r.Select(a => a.FolderId)).Distinct().ToList();

            var toDeleteFiles = Query(r => r.Files).Where(r => r.Id == (int)fileId);
            FilesDbContext.RemoveRange(toDeleteFiles);

            var toDeleteLinks = Query(r => r.TagLink).Where(r => r.EntryId == fileId.ToString()).Where(r => r.EntryType == FileEntryType.File);
            FilesDbContext.RemoveRange(toDeleteFiles);

            var tagsToRemove = Query(r => r.Tag)
                .Where(r => !Query(a => a.TagLink).Where(a => a.TagId == r.Id).Any());

            FilesDbContext.Tag.RemoveRange(tagsToRemove);

            var securityToDelete = Query(r => r.Security)
                .Where(r => r.EntryId == fileId.ToString())
                .Where(r => r.EntryType == FileEntryType.File);

            FilesDbContext.Security.RemoveRange(securityToDelete);
            FilesDbContext.SaveChanges();

            tx.Commit();

            fromFolders.ForEach(folderId => RecalculateFilesCount(folderId));

            if (deleteFolder)
                DeleteFolder(fileId);

            var wrapper = ServiceProvider.GetService<FilesWrapper>();
            wrapper.Id = (int)fileId;
            FactoryIndexer.DeleteAsync(wrapper);
        }

        public bool IsExist(string title, object folderId)
        {
            return Query(r => r.Files)
                .Where(r => r.Title == title)
                .Where(r => r.FolderId == (int)folderId)
                .Where(r => r.CurrentVersion)
                .Any();
        }

        public object MoveFile(object fileId, object toFolderId)
        {
            if (fileId == null) return null;

            using (var tx = FilesDbContext.Database.BeginTransaction())
            {
                var fromFolders = Query(r => r.Files)
                    .Where(r => r.Id == (int)fileId)
                    .GroupBy(r => r.Id)
                    .SelectMany(r => r.Select(a => a.FolderId))
                    .Distinct()
                    .ToList();

                var toUpdate = Query(r => r.Files)
                    .Where(r => r.Id == (int)fileId);

                foreach (var f in toUpdate)
                {
                    f.FolderId = (int)toFolderId;

                    if (GlobalFolder.GetFolderTrash(FolderDao).Equals(toFolderId))
                    {
                        f.ModifiedBy = AuthContext.CurrentAccount.ID;
                        f.ModifiedOn = DateTime.UtcNow;
                    }
                }

                FilesDbContext.SaveChanges();
                tx.Commit();

                fromFolders.ForEach(folderId => RecalculateFilesCount(folderId));
                RecalculateFilesCount(toFolderId);
            }

            var parentFoldersIds =
                FilesDbContext.Tree
                .Where(r => r.FolderId == (int)toFolderId)
                .OrderByDescending(r => r.Level)
                .Select(r => r.ParentId)
                .ToList();

            var wrapper = ServiceProvider.GetService<FilesWrapper>();
            wrapper.Id = (int)fileId;
            wrapper.Folders = parentFoldersIds.Select(r => new FilesFoldersWrapper() { FolderId = r.ToString() }).ToList();

            FactoryIndexer.Update(wrapper,
                UpdateAction.Replace,
                w => w.Folders);

            return fileId;
        }

        public File CopyFile(object fileId, object toFolderId)
        {
            var file = GetFile(fileId);
            if (file != null)
            {
                var copy = ServiceProvider.GetService<File>();
                copy.FileStatus = file.FileStatus;
                copy.FolderID = toFolderId;
                copy.Title = file.Title;
                copy.ConvertedType = file.ConvertedType;
                copy.Comment = FilesCommonResource.CommentCopy;
                copy.Encrypted = file.Encrypted;

                using (var stream = GetFileStream(file))
                {
                    copy.ContentLength = stream.CanSeek ? stream.Length : file.ContentLength;
                    copy = SaveFile(copy, stream);
                }

                return copy;
            }
            return null;
        }

        public object FileRename(File file, string newTitle)
        {
            newTitle = Global.ReplaceInvalidCharsAndTruncate(newTitle);
            var toUpdate = Query(r => r.Files)
                .Where(r => r.Id == (int)file.ID)
                .Where(r => r.CurrentVersion)
                .FirstOrDefault();

            toUpdate.Title = newTitle;
            toUpdate.ModifiedOn = DateTime.UtcNow;
            toUpdate.ModifiedBy = AuthContext.CurrentAccount.ID;

            FilesDbContext.SaveChanges();

            return file.ID;
        }

        public string UpdateComment(object fileId, int fileVersion, string comment)
        {
            comment ??= string.Empty;
            comment = comment.Substring(0, Math.Min(comment.Length, 255));

            var toUpdate = Query(r => r.Files)
                .Where(r => r.Id == (int)fileId)
                .Where(r => r.Version == fileVersion)
                .FirstOrDefault();

            toUpdate.Comment = comment;

            FilesDbContext.SaveChanges();

            return comment;
        }

        public void CompleteVersion(object fileId, int fileVersion)
        {
            var toUpdate = Query(r => r.Files)
                .Where(r => r.Id == (int)fileId)
                .Where(r => r.Version >= fileVersion);

            foreach (var f in toUpdate)
            {
                f.VersionGroup += 1;
            }

            FilesDbContext.SaveChanges();
        }

        public void ContinueVersion(object fileId, int fileVersion)
        {
            using var tx = FilesDbContext.Database.BeginTransaction();

            var versionGroup = Query(r => r.Files)
                .Where(r => r.Id == (int)fileId)
                .Where(r => r.Version == fileVersion)
                .Select(r => r.VersionGroup)
                .FirstOrDefault();

            var toUpdate = Query(r => r.Files)
                .Where(r => r.Id == (int)fileId)
                .Where(r => r.Version >= fileVersion)
                .Where(r => r.VersionGroup >= versionGroup);

            foreach (var f in toUpdate)
            {
                f.VersionGroup -= 1;
            }

            FilesDbContext.SaveChanges();

            tx.Commit();
        }

        public bool UseTrashForRemove(File file)
        {
            return file.RootFolderType != FolderType.TRASH;
        }

        public static string GetUniqFileDirectory(object fileIdObject)
        {
            if (fileIdObject == null) throw new ArgumentNullException("fileIdObject");
            var fileIdInt = Convert.ToInt32(Convert.ToString(fileIdObject));
            return string.Format("folder_{0}/file_{1}", (fileIdInt / 1000 + 1) * 1000, fileIdInt);
        }

        public static string GetUniqFilePath(File file)
        {
            return file != null
                       ? GetUniqFilePath(file, "content" + FileUtility.GetFileExtension(file.PureTitle))
                       : null;
        }

        public static string GetUniqFilePath(File file, string fileTitle)
        {
            return file != null
                       ? string.Format("{0}/{1}", GetUniqFileVersionPath(file.ID, file.Version), fileTitle)
                       : null;
        }

        public static string GetUniqFileVersionPath(object fileIdObject, int version)
        {
            return fileIdObject != null
                       ? string.Format("{0}/v{1}", GetUniqFileDirectory(fileIdObject), version)
                       : null;
        }

        private void RecalculateFilesCount(object folderId)
        {
            GetRecalculateFilesCountUpdate(folderId);
        }

        #region chunking

        public ChunkedUploadSession CreateUploadSession(File file, long contentLength)
        {
            return ChunkedUploadSessionHolder.CreateUploadSession(file, contentLength);
        }

        public void UploadChunk(ChunkedUploadSession uploadSession, Stream stream, long chunkLength)
        {
            if (!uploadSession.UseChunks)
            {
                using (var streamToSave = ChunkedUploadSessionHolder.UploadSingleChunk(uploadSession, stream, chunkLength))
                {
                    if (streamToSave != Stream.Null)
                    {
                        uploadSession.File = SaveFile(GetFileForCommit(uploadSession), streamToSave);
                    }
                }

                return;
            }

            ChunkedUploadSessionHolder.UploadChunk(uploadSession, stream, chunkLength);

            if (uploadSession.BytesUploaded == uploadSession.BytesTotal)
            {
                uploadSession.File = FinalizeUploadSession(uploadSession);
            }
        }

        private File FinalizeUploadSession(ChunkedUploadSession uploadSession)
        {
            ChunkedUploadSessionHolder.FinalizeUploadSession(uploadSession);

            var file = GetFileForCommit(uploadSession);
            SaveFile(file, null);
            ChunkedUploadSessionHolder.Move(uploadSession, GetUniqFilePath(file));

            return file;
        }

        public void AbortUploadSession(ChunkedUploadSession uploadSession)
        {
            ChunkedUploadSessionHolder.AbortUploadSession(uploadSession);
        }

        private File GetFileForCommit(ChunkedUploadSession uploadSession)
        {
            if (uploadSession.File.ID != null)
            {
                var file = GetFile(uploadSession.File.ID);
                file.Version++;
                file.ContentLength = uploadSession.BytesTotal;
                file.ConvertedType = null;
                file.Comment = FilesCommonResource.CommentUpload;
                file.Encrypted = uploadSession.Encrypted;
                return file;
            }
            var result = ServiceProvider.GetService<File>();
            result.FolderID = uploadSession.File.FolderID;
            result.Title = uploadSession.File.Title;
            result.ContentLength = uploadSession.BytesTotal;
            result.Comment = FilesCommonResource.CommentUpload;
            result.Encrypted = uploadSession.Encrypted;

            return result;
        }

        #endregion

        #region Only in TMFileDao

        public void ReassignFiles(object[] fileIds, Guid newOwnerId)
        {
            var toUpdate = Query(r => r.Files)
                .Where(r => r.CurrentVersion)
                .Where(r => fileIds.Any(a => a == (object)r.Id));

            foreach (var f in toUpdate)
            {
                f.CreateBy = newOwnerId;
            }

            FilesDbContext.SaveChanges();
        }

        public List<File> GetFiles(object[] parentIds, FilterType filterType, bool subjectGroup, Guid subjectID, string searchText, bool searchInContent)
        {
            if (parentIds == null || parentIds.Length == 0 || filterType == FilterType.FoldersOnly) return new List<File>();

            var q = GetFileQuery(r => r.CurrentVersion)
                .Join(FilesDbContext.Tree, a => a.FolderId, t => t.FolderId, (file, tree) => new { file, tree })
                .Where(r => parentIds.Any(a => a == (object)r.tree.ParentId))
                .Select(r => r.file);

            if (!string.IsNullOrEmpty(searchText))
            {
                var func = GetFuncForSearch(null, null, filterType, subjectGroup, subjectID, searchText, searchInContent, false);

                if (FactoryIndexer.TrySelectIds(s => func(s), out var searchIds))
                {
                    q = q.Where(r => searchIds.Any(b => b == r.Id));
                }
                else
                {
                    q = q.Where(r => BuildSearch(r, searchText, SearhTypeEnum.Any));
                }
            }

            if (subjectID != Guid.Empty)
            {
                if (subjectGroup)
                {
                    var users = UserManager.GetUsersByGroup(subjectID).Select(u => u.ID).ToArray();
                    q = q.Where(r => users.Any(u => u == r.CreateBy));
                }
                else
                {
                    q = q.Where(r => r.CreateBy == subjectID);
                }
            }

            switch (filterType)
            {
                case FilterType.DocumentsOnly:
                case FilterType.ImagesOnly:
                case FilterType.PresentationsOnly:
                case FilterType.SpreadsheetsOnly:
                case FilterType.ArchiveOnly:
                case FilterType.MediaOnly:
                    q = q.Where(r => r.Category == (int)filterType);
                    break;
                case FilterType.ByExtension:
                    if (!string.IsNullOrEmpty(searchText))
                    {
                        q = q.Where(r => BuildSearch(r, searchText, SearhTypeEnum.End));
                    }
                    break;
            }

            return FromQuery(q);
        }

        public IEnumerable<File> Search(string searchText, bool bunch)
        {
            if (FactoryIndexer.TrySelectIds(s => s.MatchAll(searchText), out var ids))
            {
                var query = GetFileQuery(r => r.CurrentVersion && ids.Any(i => i == r.Id));
                return FromQuery(query)
                    .Where(
                        f =>
                        bunch
                            ? f.RootFolderType == FolderType.BUNCH
                            : f.RootFolderType == FolderType.USER || f.RootFolderType == FolderType.COMMON)
                    .ToList();
            }
            else
            {
                var query = GetFileQuery(r => r.CurrentVersion && BuildSearch(r, searchText, SearhTypeEnum.Any));
                return FromQuery(query)
                    .Where(f =>
                           bunch
                                ? f.RootFolderType == FolderType.BUNCH
                                : f.RootFolderType == FolderType.USER || f.RootFolderType == FolderType.COMMON)
                    .ToList();
            }
        }

        private void DeleteFolder(object fileId)
        {
            GlobalStore.GetStore().DeleteDirectory(GetUniqFileDirectory(fileId));
        }

        public bool IsExistOnStorage(File file)
        {
            return GlobalStore.GetStore().IsFile(GetUniqFilePath(file));
        }

        private const string DiffTitle = "diff.zip";

        public void SaveEditHistory(File file, string changes, Stream differenceStream)
        {
            if (file == null) throw new ArgumentNullException("file");
            if (string.IsNullOrEmpty(changes)) throw new ArgumentNullException("changes");
            if (differenceStream == null) throw new ArgumentNullException("differenceStream");

            changes = changes.Trim();

            var toUpdate = Query(r => r.Files)
                .Where(r => r.Id == (int)file.ID)
                .Where(r => r.Version == file.Version);

            foreach (var f in toUpdate)
            {
                f.Changes = changes;
            }

            FilesDbContext.SaveChanges();

            GlobalStore.GetStore().Save(string.Empty, GetUniqFilePath(file, DiffTitle), differenceStream, DiffTitle);
        }

        public List<EditHistory> GetEditHistory(DocumentServiceHelper documentServiceHelper, object fileId, int fileVersion = 0)
        {
            var query = Query(r => r.Files)
                .Where(r => r.Id == (int)fileId)
                .Where(r => r.Forcesave == ForcesaveType.None);

            if (fileVersion > 0)
            {
                query = query.Where(r => r.Version == fileVersion);
            }

            query = query.OrderBy(r => r.Version);

            return query
                    .ToList()
                    .Select(r =>
                        {
                            var item = ServiceProvider.GetService<EditHistory>();
                            var editHistoryAuthor = ServiceProvider.GetService<EditHistoryAuthor>();

                            editHistoryAuthor.Id = r.ModifiedBy;
                            item.ID = r.Id;
                            item.Version = r.Version;
                            item.VersionGroup = r.VersionGroup;
                            item.ModifiedOn = TenantUtil.DateTimeFromUtc(r.ModifiedOn);
                            item.ModifiedBy = editHistoryAuthor;
                            item.ChangesString = r.Changes;
                            item.Key = documentServiceHelper.GetDocKey(item.ID, item.Version, TenantUtil.DateTimeFromUtc(r.CreateOn));

                            return item;
                        })
                    .ToList();
        }

        public Stream GetDifferenceStream(File file)
        {
            return GlobalStore.GetStore().GetReadStream(string.Empty, GetUniqFilePath(file, DiffTitle));
        }

        public bool ContainChanges(object fileId, int fileVersion)
        {
            return Query(r => r.Files)
                .Where(r => r.Id == (int)fileId)
                .Where(r => r.Version == fileVersion)
                .Where(r => r.Changes != null)
                .Any();
        }

        #endregion

        private static ForcesaveType ParseForcesaveType(object v)
        {
            return v != null
                       ? (ForcesaveType)Enum.Parse(typeof(ForcesaveType), v.ToString().Substring(0, 1))
                       : default;
        }

        private Func<Selector<FilesWrapper>, Selector<FilesWrapper>> GetFuncForSearch(object parentId, OrderBy orderBy, FilterType filterType, bool subjectGroup, Guid subjectID, string searchText, bool searchInContent, bool withSubfolders = false)
        {
            return s =>
           {
               var result = !searchInContent || filterType == FilterType.ByExtension
                   ? s.Match(r => r.Title, searchText)
                   : s.MatchAll(searchText);

               if (parentId != null)
               {
                   if (withSubfolders)
                   {
                       result.In(a => a.Folders.Select(r => r.FolderId), new[] { parentId.ToString() });
                   }
                   else
                   {
                       result.InAll(a => a.Folders.Select(r => r.FolderId), new[] { parentId.ToString() });
                   }
               }

               if (orderBy != null)
               {
                   switch (orderBy.SortedBy)
                   {
                       case SortedByType.Author:
                           result.Sort(r => r.CreateBy, orderBy.IsAsc);
                           break;
                       case SortedByType.Size:
                           result.Sort(r => r.ContentLength, orderBy.IsAsc);
                           break;
                       //case SortedByType.AZ:
                       //    result.Sort(r => r.Title, orderBy.IsAsc);
                       //    break;
                       case SortedByType.DateAndTime:
                           result.Sort(r => r.LastModifiedOn, orderBy.IsAsc);
                           break;
                       case SortedByType.DateAndTimeCreation:
                           result.Sort(r => r.CreateOn, orderBy.IsAsc);
                           break;
                   }
               }

               if (subjectID != Guid.Empty)
               {
                   if (subjectGroup)
                   {
                       var users = UserManager.GetUsersByGroup(subjectID).Select(u => u.ID.ToString()).ToArray();
                       result.In(r => r.CreateBy, users);
                   }
                   else
                   {
                       result.Where(r => r.CreateBy, subjectID);
                   }
               }

               switch (filterType)
               {
                   case FilterType.DocumentsOnly:
                   case FilterType.ImagesOnly:
                   case FilterType.PresentationsOnly:
                   case FilterType.SpreadsheetsOnly:
                   case FilterType.ArchiveOnly:
                   case FilterType.MediaOnly:
                       result.Where(r => r.Category, (int)filterType);
                       break;
               }

               return result;
           };
        }
    }
}