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
using System.Linq;
using System.Security;

using ASC.Common;
using ASC.Common.Caching;
using ASC.Common.Threading.Workers;
using ASC.Core;
using ASC.Core.Users;
using ASC.Files.Core;
using ASC.Files.Core.Data;
using ASC.Files.Core.Security;
using ASC.Files.Resources;
using ASC.Web.Files.Classes;

using Microsoft.Extensions.DependencyInjection;

using static ASC.Web.Files.Utils.FileMarker;

using File = ASC.Files.Core.File;

namespace ASC.Web.Files.Utils
{
    public class FileMarker
    {
        private static readonly object locker = new object();
        private readonly WorkerQueue<AsyncTaskData> tasks;
        private readonly ICache cache;

        private const string CacheKeyFormat = "MarkedAsNew/{0}/folder_{1}";

        public TenantManager TenantManager { get; }
        public UserManager UserManager { get; }
        public IDaoFactory DaoFactory { get; }
        public GlobalFolder GlobalFolder { get; }
        public FileSecurity FileSecurity { get; }
        public CoreBaseSettings CoreBaseSettings { get; }
        public AuthContext AuthContext { get; }
        public IServiceProvider ServiceProvider { get; }

        public FileMarker(
            TenantManager tenantManager,
            UserManager userManager,
            IDaoFactory daoFactory,
            GlobalFolder globalFolder,
            FileSecurity fileSecurity,
            CoreBaseSettings coreBaseSettings,
            AuthContext authContext,
            IServiceProvider serviceProvider,
            WorkerQueueOptionsManager<AsyncTaskData> workerQueueOptionsManager)
        {
            TenantManager = tenantManager;
            UserManager = userManager;
            DaoFactory = daoFactory;
            GlobalFolder = globalFolder;
            FileSecurity = fileSecurity;
            CoreBaseSettings = coreBaseSettings;
            AuthContext = authContext;
            ServiceProvider = serviceProvider;
            cache = AscCache.Memory;
            tasks = workerQueueOptionsManager.Value;
        }

        private void ExecMarkFileAsNew(AsyncTaskData obj)
        {
            TenantManager.SetCurrentTenant(Convert.ToInt32(obj.TenantID));

            var folderDao = DaoFactory.FolderDao;
            object parentFolderId;

            if (obj.FileEntry.FileEntryType == FileEntryType.File)
                parentFolderId = ((File)obj.FileEntry).FolderID;
            else
                parentFolderId = obj.FileEntry.ID;
            var parentFolders = folderDao.GetParentFolders(parentFolderId);
            parentFolders.Reverse();

            var userIDs = obj.UserIDs;

            var userEntriesData = new Dictionary<Guid, List<FileEntry>>();

            if (obj.FileEntry.RootFolderType == FolderType.BUNCH)
            {
                if (!userIDs.Any()) return;

                parentFolders.Add(folderDao.GetFolder(GlobalFolder.GetFolderProjects(DaoFactory)));

                var entries = new List<FileEntry> { obj.FileEntry };
                entries = entries.Concat(parentFolders).ToList();

                userIDs.ForEach(userID =>
                                        {
                                            if (userEntriesData.ContainsKey(userID))
                                                userEntriesData[userID].AddRange(entries);
                                            else
                                                userEntriesData.Add(userID, entries);

                                            RemoveFromCahce(GlobalFolder.GetFolderProjects(DaoFactory), userID);
                                        });
            }
            else
            {
                var filesSecurity = FileSecurity;

                if (!userIDs.Any())
                {
                    userIDs = filesSecurity.WhoCanRead(obj.FileEntry).Where(x => x != obj.CurrentAccountId).ToList();
                }
                if (obj.FileEntry.ProviderEntry)
                {
                    userIDs = userIDs.Where(u => !UserManager.GetUsers(u).IsVisitor(UserManager)).ToList();
                }

                parentFolders.ForEach(parentFolder =>
                                      filesSecurity
                                          .WhoCanRead(parentFolder)
                                          .Where(userID => userIDs.Contains(userID) && userID != obj.CurrentAccountId)
                                          .ToList()
                                          .ForEach(userID =>
                                                       {
                                                           if (userEntriesData.ContainsKey(userID))
                                                               userEntriesData[userID].Add(parentFolder);
                                                           else
                                                               userEntriesData.Add(userID, new List<FileEntry> { parentFolder });
                                                       })
                    );



                if (obj.FileEntry.RootFolderType == FolderType.USER)
                {
                    var folderShare = folderDao.GetFolder(GlobalFolder.GetFolderShare(DaoFactory.FolderDao));

                    foreach (var userID in userIDs)
                    {
                        var userFolderId = folderDao.GetFolderIDUser(false, userID);
                        if (Equals(userFolderId, 0)) continue;

                        Folder rootFolder = null;
                        if (obj.FileEntry.ProviderEntry)
                        {
                            rootFolder = obj.FileEntry.RootFolderCreator == userID
                                             ? folderDao.GetFolder(userFolderId)
                                             : folderShare;
                        }
                        else if (!Equals(obj.FileEntry.RootFolderId, userFolderId))
                        {
                            rootFolder = folderShare;
                        }
                        else
                        {
                            RemoveFromCahce(userFolderId, userID);
                        }

                        if (rootFolder == null) continue;

                        if (userEntriesData.ContainsKey(userID))
                            userEntriesData[userID].Add(rootFolder);
                        else
                            userEntriesData.Add(userID, new List<FileEntry> { rootFolder });

                        RemoveFromCahce(rootFolder.ID, userID);
                    }
                }

                if (obj.FileEntry.RootFolderType == FolderType.COMMON)
                {
                    userIDs.ForEach(userID => RemoveFromCahce(GlobalFolder.GetFolderCommon(this, DaoFactory), userID));

                    if (obj.FileEntry.ProviderEntry)
                    {
                        var commonFolder = folderDao.GetFolder(GlobalFolder.GetFolderCommon(this, DaoFactory));
                        userIDs.ForEach(userID =>
                                            {
                                                if (userEntriesData.ContainsKey(userID))
                                                    userEntriesData[userID].Add(commonFolder);
                                                else
                                                    userEntriesData.Add(userID, new List<FileEntry> { commonFolder });

                                                RemoveFromCahce(GlobalFolder.GetFolderCommon(this, DaoFactory), userID);
                                            });
                    }
                }

                userIDs.ForEach(userID =>
                                    {
                                        if (userEntriesData.ContainsKey(userID))
                                            userEntriesData[userID].Add(obj.FileEntry);
                                        else
                                            userEntriesData.Add(userID, new List<FileEntry> { obj.FileEntry });
                                    });
            }

            var tagDao = DaoFactory.TagDao;
            var newTags = new List<Tag>();
            var updateTags = new List<Tag>();

            foreach (var userID in userEntriesData.Keys)
            {
                if (tagDao.GetNewTags(userID, obj.FileEntry).Any())
                    continue;

                var entries = userEntriesData[userID].Distinct().ToList();

                var exist = tagDao.GetNewTags(userID, entries).ToList();
                var update = exist.Where(t => t.EntryType == FileEntryType.Folder).ToList();
                update.ForEach(t => t.Count++);
                updateTags.AddRange(update);

                entries.ForEach(entry =>
                                    {
                                        if (entry != null && exist.All(tag => tag != null && !tag.EntryId.Equals(entry.ID)))
                                        {
                                            newTags.Add(Tag.New(userID, entry));
                                        }
                                    });
            }

            if (updateTags.Any())
                tagDao.UpdateNewTags(updateTags);
            if (newTags.Any())
                tagDao.SaveTags(newTags);
        }

        public void MarkAsNew(FileEntry fileEntry, List<Guid> userIDs = null)
        {
            if (CoreBaseSettings.Personal) return;

            if (fileEntry == null) return;
            userIDs ??= new List<Guid>();

            var taskData = ServiceProvider.GetService<AsyncTaskData>();
            taskData.FileEntry = (FileEntry)fileEntry.Clone();
            taskData.UserIDs = userIDs;

            if (fileEntry.RootFolderType == FolderType.BUNCH && !userIDs.Any())
            {
                var folderDao = DaoFactory.FolderDao;
                var path = folderDao.GetBunchObjectID(fileEntry.RootFolderId);

                var projectID = path.Split('/').Last();
                if (string.IsNullOrEmpty(projectID)) return;

                var projectTeam = FileSecurity.WhoCanRead(fileEntry)
                                        .Where(x => x != AuthContext.CurrentAccount.ID).ToList();

                if (!projectTeam.Any()) return;

                taskData.UserIDs = projectTeam;
            }

            lock (locker)
            {
                tasks.Add(taskData);

                if (!tasks.IsStarted)
                    tasks.Start(ExecMarkFileAsNew);
            }
        }

        public void RemoveMarkAsNew(FileEntry fileEntry, Guid userID = default)
        {
            if (CoreBaseSettings.Personal) return;

            userID = userID.Equals(default) ? AuthContext.CurrentAccount.ID : userID;

            if (fileEntry == null) return;

            var tagDao = DaoFactory.TagDao;
            var folderDao = DaoFactory.FolderDao;
            if (!tagDao.GetNewTags(userID, fileEntry).Any()) return;

            object folderID;
            int valueNew;
            var userFolderId = folderDao.GetFolderIDUser(false, userID);

            var removeTags = new List<Tag>();

            if (fileEntry.FileEntryType == FileEntryType.File)
            {
                folderID = ((File)fileEntry).FolderID;

                removeTags.Add(Tag.New(userID, fileEntry));
                valueNew = 1;
            }
            else
            {
                folderID = fileEntry.ID;

                var listTags = tagDao.GetNewTags(userID, (Folder)fileEntry, true).ToList();
                valueNew = listTags.FirstOrDefault(tag => tag.EntryId.Equals(fileEntry.ID)).Count;

                if (Equals(fileEntry.ID, userFolderId) || Equals(fileEntry.ID, GlobalFolder.GetFolderCommon(this, DaoFactory)) || Equals(fileEntry.ID, GlobalFolder.GetFolderShare(DaoFactory.FolderDao)))
                {
                    var folderTags = listTags.Where(tag => tag.EntryType == FileEntryType.Folder);

                    var providerFolderTags = folderTags.Select(tag => new KeyValuePair<Tag, Folder>(tag, folderDao.GetFolder(tag.EntryId)))
                                                       .Where(pair => pair.Value != null && pair.Value.ProviderEntry).ToList();

                    foreach (var providerFolderTag in providerFolderTags)
                    {
                        listTags.Remove(providerFolderTag.Key);
                        listTags.AddRange(tagDao.GetNewTags(userID, providerFolderTag.Value, true));
                    }
                }

                removeTags.AddRange(listTags);
            }

            var parentFolders = folderDao.GetParentFolders(folderID);
            parentFolders.Reverse();

            var rootFolder = parentFolders.LastOrDefault();
            object rootFolderId = null;
            object cacheFolderId = null;
            if (rootFolder == null)
            {
            }
            else if (rootFolder.RootFolderType == FolderType.BUNCH)
            {
                cacheFolderId = rootFolderId = GlobalFolder.GetFolderProjects(DaoFactory);
            }
            else if (rootFolder.RootFolderType == FolderType.COMMON)
            {
                if (rootFolder.ProviderEntry)
                    cacheFolderId = rootFolderId = GlobalFolder.GetFolderCommon(this, DaoFactory);
                else
                    cacheFolderId = GlobalFolder.GetFolderCommon(this, DaoFactory);
            }
            else if (rootFolder.RootFolderType == FolderType.USER)
            {
                if (rootFolder.ProviderEntry && rootFolder.RootFolderCreator == userID)
                    cacheFolderId = rootFolderId = userFolderId;
                else if (!rootFolder.ProviderEntry && !Equals(rootFolder.RootFolderId, userFolderId)
                         || rootFolder.ProviderEntry && rootFolder.RootFolderCreator != userID)
                    cacheFolderId = rootFolderId = GlobalFolder.GetFolderShare(DaoFactory.FolderDao);
                else
                    cacheFolderId = userFolderId;
            }
            else if (rootFolder.RootFolderType == FolderType.SHARE)
            {
                cacheFolderId = GlobalFolder.GetFolderShare(DaoFactory.FolderDao);
            }

            if (rootFolderId != null)
            {
                parentFolders.Add(folderDao.GetFolder(rootFolderId));
            }
            if (cacheFolderId != null)
            {
                RemoveFromCahce(cacheFolderId, userID);
            }

            var updateTags = new List<Tag>();
            foreach (var parentFolder in parentFolders)
            {
                var parentTag = tagDao.GetNewTags(userID, parentFolder).FirstOrDefault();

                if (parentTag != null)
                {
                    parentTag.Count -= valueNew;

                    if (parentTag.Count > 0)
                    {
                        updateTags.Add(parentTag);
                    }
                    else
                    {
                        removeTags.Add(parentTag);
                    }
                }
            }

            if (updateTags.Any())
                tagDao.UpdateNewTags(updateTags);
            if (removeTags.Any())
                tagDao.RemoveTags(removeTags);
        }

        public void RemoveMarkAsNewForAll(FileEntry fileEntry)
        {
            List<Guid> userIDs;

            var tagDao = DaoFactory.TagDao;
            var tags = tagDao.GetTags(fileEntry.ID, fileEntry.FileEntryType == FileEntryType.File ? FileEntryType.File : FileEntryType.Folder, TagType.New);
            userIDs = tags.Select(tag => tag.Owner).Distinct().ToList();

            foreach (var userID in userIDs)
            {
                RemoveMarkAsNew(fileEntry, userID);
            }
        }

        public Dictionary<object, int> GetRootFoldersIdMarkedAsNew()
        {
            var rootIds = new List<object>
                {
                    GlobalFolder.GetFolderMy(this, DaoFactory),
                    GlobalFolder.GetFolderCommon(this, DaoFactory),
                    GlobalFolder.GetFolderShare(DaoFactory.FolderDao),
                    GlobalFolder.GetFolderProjects(DaoFactory)
                };

            var requestIds = new List<object>();
            var news = new Dictionary<object, int>();

            rootIds.ForEach(rootId =>
                                {
                                    var fromCache = GetCountFromCahce(rootId);
                                    if (fromCache == -1)
                                    {
                                        requestIds.Add(rootId);
                                    }
                                    else if ((fromCache) > 0)
                                    {
                                        news.Add(rootId, (int)fromCache);
                                    }
                                });

            if (requestIds.Any())
            {
                IEnumerable<Tag> requestTags;
                var tagDao = DaoFactory.TagDao;
                var folderDao = DaoFactory.FolderDao;
                requestTags = tagDao.GetNewTags(AuthContext.CurrentAccount.ID, folderDao.GetFolders(requestIds.ToArray()));

                requestIds.ForEach(requestId =>
                                       {
                                           var requestTag = requestTags.FirstOrDefault(tag => tag.EntryId.Equals(requestId));
                                           InsertToCahce(requestId, requestTag == null ? 0 : requestTag.Count);
                                       });

                news = news.Concat(requestTags.ToDictionary(x => x.EntryId, x => x.Count)).ToDictionary(x => x.Key, x => x.Value);
            }

            return news;
        }

        public List<FileEntry> MarkedItems(Folder folder)
        {
            if (folder == null) throw new ArgumentNullException("folder", FilesCommonResource.ErrorMassage_FolderNotFound);
            if (!FileSecurity.CanRead(folder)) throw new SecurityException(FilesCommonResource.ErrorMassage_SecurityException_ViewFolder);
            if (folder.RootFolderType == FolderType.TRASH && !Equals(folder.ID, GlobalFolder.GetFolderTrash(DaoFactory.FolderDao))) throw new SecurityException(FilesCommonResource.ErrorMassage_ViewTrashItem);

            var entryTags = new Dictionary<FileEntry, Tag>();

            var tagDao = DaoFactory.TagDao;
            var fileDao = DaoFactory.FileDao;
            var folderDao = DaoFactory.FolderDao;
            var tags = (tagDao.GetNewTags(AuthContext.CurrentAccount.ID, folder, true) ?? new List<Tag>()).ToList();

            if (!tags.Any()) return new List<FileEntry>();

            if (Equals(folder.ID, GlobalFolder.GetFolderMy(this, DaoFactory)) || Equals(folder.ID, GlobalFolder.GetFolderCommon(this, DaoFactory)) || Equals(folder.ID, GlobalFolder.GetFolderShare(DaoFactory.FolderDao)))
            {
                var folderTags = tags.Where(tag => tag.EntryType == FileEntryType.Folder);

                var providerFolderTags = folderTags.Select(tag => new KeyValuePair<Tag, Folder>(tag, folderDao.GetFolder(tag.EntryId)))
                                                    .Where(pair => pair.Value != null && pair.Value.ProviderEntry).ToList();
                providerFolderTags.Reverse();

                foreach (var providerFolderTag in providerFolderTags)
                {
                    tags.AddRange(tagDao.GetNewTags(AuthContext.CurrentAccount.ID, providerFolderTag.Value, true));
                }
            }

            tags = tags.Distinct().ToList();
            tags.RemoveAll(tag => Equals(tag.EntryId, folder.ID));
            tags = tags.Where(t => t.EntryType == FileEntryType.Folder)
                        .Concat(tags.Where(t => t.EntryType == FileEntryType.File)).ToList();

            foreach (var tag in tags)
            {
                var entry = tag.EntryType == FileEntryType.File
                                ? (FileEntry)fileDao.GetFile(tag.EntryId)
                                : (FileEntry)folderDao.GetFolder(tag.EntryId);
                if (entry != null)
                {
                    entryTags.Add(entry, tag);
                }
                else
                {
                    //todo: RemoveMarkAsNew(tag);
                }
            }

            foreach (var entryTag in entryTags)
            {
                var entry = entryTag.Key;
                var parentId =
                    entry.FileEntryType == FileEntryType.File
                        ? ((File)entry).FolderID
                        : ((Folder)entry).ParentFolderID;

                var parentEntry = entryTags.Keys.FirstOrDefault(entryCountTag => Equals(entryCountTag.ID, parentId));
                if (parentEntry != null)
                    entryTags[parentEntry].Count -= entryTag.Value.Count;
            }

            var result = new List<FileEntry>();

            foreach (var entryTag in entryTags)
            {
                if (!string.IsNullOrEmpty(entryTag.Key.Error))
                {
                    RemoveMarkAsNew(entryTag.Key);
                    continue;
                }

                if (entryTag.Value.Count > 0)
                {
                    result.Add(entryTag.Key);
                }
            }
            return result;
        }

        public IEnumerable<FileEntry> SetTagsNew(Folder parent, IEnumerable<FileEntry> entries)
        {
            var tagDao = DaoFactory.TagDao;
            var folderDao = DaoFactory.FolderDao;
            var totalTags = tagDao.GetNewTags(AuthContext.CurrentAccount.ID, parent, false).ToList();

            if (totalTags.Any())
            {
                var parentFolderTag = Equals(GlobalFolder.GetFolderShare(DaoFactory.FolderDao), parent.ID)
                                            ? tagDao.GetNewTags(AuthContext.CurrentAccount.ID, folderDao.GetFolder(GlobalFolder.GetFolderShare(DaoFactory.FolderDao))).FirstOrDefault()
                                            : totalTags.FirstOrDefault(tag => tag.EntryType == FileEntryType.Folder && Equals(tag.EntryId, parent.ID));

                totalTags.Remove(parentFolderTag);
                var countSubNew = 0;
                totalTags.ForEach(tag => countSubNew += tag.Count);

                if (parentFolderTag == null)
                {
                    parentFolderTag = Tag.New(AuthContext.CurrentAccount.ID, parent, 0);
                    parentFolderTag.Id = -1;
                }

                if (parentFolderTag.Count != countSubNew)
                {
                    if (countSubNew > 0)
                    {
                        var diff = parentFolderTag.Count - countSubNew;

                        parentFolderTag.Count -= diff;
                        if (parentFolderTag.Id == -1)
                        {
                            tagDao.SaveTags(parentFolderTag);
                        }
                        else
                        {
                            tagDao.UpdateNewTags(parentFolderTag);
                        }

                        var cacheFolderId = parent.ID;
                        var parentsList = DaoFactory.FolderDao.GetParentFolders(parent.ID);
                        parentsList.Reverse();
                        parentsList.Remove(parent);

                        if (parentsList.Any())
                        {
                            var rootFolder = parentsList.Last();
                            object rootFolderId = null;
                            cacheFolderId = rootFolder.ID;
                            if (rootFolder.RootFolderType == FolderType.BUNCH)
                                cacheFolderId = rootFolderId = GlobalFolder.GetFolderProjects(DaoFactory);
                            else if (rootFolder.RootFolderType == FolderType.USER && !Equals(rootFolder.RootFolderId, GlobalFolder.GetFolderMy(this, DaoFactory)))
                                cacheFolderId = rootFolderId = GlobalFolder.GetFolderShare(DaoFactory.FolderDao);

                            if (rootFolderId != null)
                            {
                                parentsList.Add(DaoFactory.FolderDao.GetFolder(rootFolderId));
                            }

                            var fileSecurity = FileSecurity;

                            foreach (var folderFromList in parentsList)
                            {
                                var parentTreeTag = tagDao.GetNewTags(AuthContext.CurrentAccount.ID, folderFromList).FirstOrDefault();

                                if (parentTreeTag == null)
                                {
                                    if (fileSecurity.CanRead(folderFromList))
                                    {
                                        tagDao.SaveTags(Tag.New(AuthContext.CurrentAccount.ID, folderFromList, -diff));
                                    }
                                }
                                else
                                {
                                    parentTreeTag.Count -= diff;
                                    tagDao.UpdateNewTags(parentTreeTag);
                                }
                            }
                        }

                        if (cacheFolderId != null)
                        {
                            RemoveFromCahce(cacheFolderId);
                        }
                    }
                    else
                    {
                        RemoveMarkAsNew(parent);
                    }
                }

                entries.ToList().ForEach(
                    entry =>
                    {
                        var curTag = totalTags.FirstOrDefault(tag => tag.EntryType == entry.FileEntryType && tag.EntryId.Equals(entry.ID));

                        if (entry.FileEntryType == FileEntryType.Folder)
                        {
                            ((Folder)entry).NewForMe = curTag != null ? curTag.Count : 0;
                        }
                        else if (curTag != null)
                        {
                            entry.IsNew = true;
                        }
                    });
            }


            return entries;
        }

        private void InsertToCahce(object folderId, int count)
        {
            var key = string.Format(CacheKeyFormat, AuthContext.CurrentAccount.ID, folderId);
            cache.Insert(key, count.ToString(), TimeSpan.FromMinutes(10));
        }

        private int GetCountFromCahce(object folderId)
        {
            var key = string.Format(CacheKeyFormat, AuthContext.CurrentAccount.ID, folderId);
            var count = cache.Get<string>(key);
            return count == null ? -1 : int.Parse(count);
        }

        private void RemoveFromCahce(object folderId)
        {
            RemoveFromCahce(folderId, AuthContext.CurrentAccount.ID);
        }

        private void RemoveFromCahce(object folderId, Guid userId)
        {
            var key = string.Format(CacheKeyFormat, userId, folderId);
            cache.Remove(key);
        }


        public class AsyncTaskData
        {
            public AsyncTaskData(TenantManager tenantManager, AuthContext authContext)
            {
                TenantID = tenantManager.GetCurrentTenant().TenantId;
                CurrentAccountId = authContext.CurrentAccount.ID;
            }

            public int TenantID { get; private set; }

            public FileEntry FileEntry { get; set; }

            public List<Guid> UserIDs { get; set; }

            public Guid CurrentAccountId { get; set; }
        }
    }
    public static class FileMarkerExtention
    {
        public static DIHelper AddFileMarkerService(this DIHelper services)
        {
            services.TryAddScoped<FileMarker>();
            services.TryAddSingleton<WorkerQueueOptionsManager<AsyncTaskData>>();

            return services
                .AddTenantManagerService()
                .AddUserManagerService()
                .AddDaoFactoryService()
                .AddGlobalFolderService()
                .AddFileSecurityService()
                .AddCoreBaseSettingsService()
                .AddAuthContextService();
        }
    }
}