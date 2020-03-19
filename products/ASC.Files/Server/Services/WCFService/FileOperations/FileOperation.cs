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
using System.Globalization;
using System.Linq;
using System.Security;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

using ASC.Common.Logging;
using ASC.Common.Security.Authentication;
using ASC.Common.Security.Authorizing;
using ASC.Common.Threading;
using ASC.Core;
using ASC.Core.Tenants;
using ASC.Files.Core;
using ASC.Files.Core.Security;
using ASC.Files.Resources;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ASC.Web.Files.Services.WCFService.FileOperations
{
    public abstract class FileOperation
    {
        public const string SPLIT_CHAR = ":";
        public const string OWNER = "Owner";
        public const string OPERATION_TYPE = "OperationType";
        public const string SOURCE = "Source";
        public const string PROGRESS = "Progress";
        public const string RESULT = "Result";
        public const string ERROR = "Error";
        public const string PROCESSED = "Processed";
        public const string FINISHED = "Finished";
        public const string HOLD = "Hold";

        protected readonly IPrincipal principal;
        protected readonly string culture;
        protected int total;
        protected int processed;
        protected int successProcessed;

        public virtual FileOperationType OperationType { get; }
        public bool HoldResult { get; set; }

        protected string Status { get; set; }

        protected string Error { get; set; }

        protected DistributedTask TaskInfo { get; set; }

        public FileOperation(IServiceProvider serviceProvider)
        {
            principal = serviceProvider.GetService<Microsoft.AspNetCore.Http.IHttpContextAccessor>()?.HttpContext?.User ?? Thread.CurrentPrincipal;
            culture = Thread.CurrentThread.CurrentCulture.Name;

            TaskInfo = new DistributedTask();
        }

        public virtual DistributedTask GetDistributedTask()
        {
            FillDistributedTask();
            return TaskInfo;
        }


        protected internal virtual void FillDistributedTask()
        {
            var progress = total != 0 ? 100 * processed / total : 0;

            TaskInfo.SetProperty(OPERATION_TYPE, OperationType);
            TaskInfo.SetProperty(OWNER, ((IAccount)(principal ?? Thread.CurrentPrincipal).Identity).ID);
            TaskInfo.SetProperty(PROGRESS, progress < 100 ? progress : 100);
            TaskInfo.SetProperty(RESULT, Status);
            TaskInfo.SetProperty(ERROR, Error);
            TaskInfo.SetProperty(PROCESSED, successProcessed);
            TaskInfo.SetProperty(HOLD, HoldResult);
        }

        public abstract void RunJob(DistributedTask _, CancellationToken cancellationToken);
        protected abstract void Do(IServiceScope serviceScope);
    }

    internal class ComposeFileOperation<T1, T2> : FileOperation
        where T1 : FileOperationData<string>
        where T2 : FileOperationData<int>
    {
        public FileOperation<T1, string> F1 { get; set; }
        public FileOperation<T2, int> F2 { get; set; }

        public ComposeFileOperation(IServiceProvider serviceProvider, FileOperation<T1, string> f1, FileOperation<T2, int> f2)
            : base(serviceProvider)
        {
            F1 = f1;
            F2 = f2;
        }

        public override void RunJob(DistributedTask _, CancellationToken cancellationToken)
        {
            F1.RunJob(_, cancellationToken);
            F2.RunJob(_, cancellationToken);
        }

        protected internal override void FillDistributedTask()
        {
            F1.FillDistributedTask();
            F2.FillDistributedTask();
        }

        protected override void Do(IServiceScope serviceScope)
        {
            throw new NotImplementedException();
        }
    }

    abstract class FileOperationData<T>
    {
        public List<T> Folders { get; private set; }

        public List<T> Files { get; private set; }

        public Tenant Tenant { get; }

        public bool HoldResult { get; set; }

        protected FileOperationData(List<T> folders, List<T> files, Tenant tenant, bool holdResult = true)
        {
            Folders = folders ?? new List<T>();
            Files = files ?? new List<T>();
            Tenant = tenant;
            HoldResult = holdResult;
        }
    }

    abstract class FileOperation<T, TId> : FileOperation where T : FileOperationData<TId>
    {
        protected Tenant CurrentTenant { get; private set; }

        protected FileSecurity FilesSecurity { get; private set; }

        protected IFolderDao<TId> FolderDao { get; private set; }

        protected IFileDao<TId> FileDao { get; private set; }

        protected ITagDao<TId> TagDao { get; private set; }

        protected IProviderDao ProviderDao { get; private set; }

        protected ILog Logger { get; private set; }

        protected CancellationToken CancellationToken { get; private set; }

        protected List<TId> Folders { get; private set; }

        protected List<TId> Files { get; private set; }

        public IServiceProvider ServiceProvider { get; }

        protected FileOperation(IServiceProvider serviceProvider, T fileOperationData) : base(serviceProvider)
        {
            ServiceProvider = serviceProvider;
            Files = fileOperationData.Files;
            Folders = fileOperationData.Folders;
            HoldResult = fileOperationData.HoldResult;
            CurrentTenant = fileOperationData.Tenant;
        }

        public override void RunJob(DistributedTask _, CancellationToken cancellationToken)
        {
            try
            {
                CancellationToken = cancellationToken;

                using var scope = ServiceProvider.CreateScope();
                var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
                tenantManager.SetCurrentTenant(CurrentTenant);
                var daoFactory = scope.ServiceProvider.GetService<IDaoFactory>();
                var fileSecurity = scope.ServiceProvider.GetService<FileSecurity>();
                var logger = scope.ServiceProvider.GetService<IOptionsMonitor<ILog>>().CurrentValue;


                Thread.CurrentPrincipal = principal;
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(culture);

                FolderDao = daoFactory.GetFolderDao<TId>();
                FileDao = daoFactory.GetFileDao<TId>();
                TagDao = daoFactory.GetTagDao<TId>();
                ProviderDao = daoFactory.ProviderDao;
                FilesSecurity = fileSecurity;

                Logger = logger;

                total = InitTotalProgressSteps();

                Do(scope);
            }
            catch (AuthorizingException authError)
            {
                Error = FilesCommonResource.ErrorMassage_SecurityException;
                Logger.Error(Error, new SecurityException(Error, authError));
            }
            catch (AggregateException ae)
            {
                ae.Flatten().Handle(e => e is TaskCanceledException || e is OperationCanceledException);
            }
            catch (Exception error)
            {
                Error = error is TaskCanceledException || error is OperationCanceledException
                            ? FilesCommonResource.ErrorMassage_OperationCanceledException
                            : error.Message;
                Logger.Error(error, error);
            }
            finally
            {
                try
                {
                    TaskInfo.SetProperty(FINISHED, true);
                    PublishTaskInfo();
                }
                catch { /* ignore */ }
            }
        }

        protected internal override void FillDistributedTask()
        {
            base.FillDistributedTask();

            TaskInfo.SetProperty(SOURCE, string.Join(SPLIT_CHAR, Folders.Select(f => "folder_" + f).Concat(Files.Select(f => "file_" + f)).ToArray()));
        }

        protected virtual int InitTotalProgressSteps()
        {
            var count = Files.Count;
            Folders.ForEach(f => count += 1 + (FolderDao.CanCalculateSubitems(f) ? FolderDao.GetItemsCount(f) : 0));
            return count;
        }

        protected void ProgressStep(TId folderId = default, TId fileId = default)
        {
            if (folderId == null && fileId == null
                || folderId != null && Folders.Contains(folderId)
                || fileId != null && Files.Contains(fileId))
            {
                processed++;
                PublishTaskInfo();
            }
        }

        protected bool ProcessedFolder(TId folderId)
        {
            successProcessed++;
            if (Folders.Contains(folderId))
            {
                Status += string.Format("folder_{0}{1}", folderId, SPLIT_CHAR);
                return true;
            }
            return false;
        }

        protected bool ProcessedFile(TId fileId)
        {
            successProcessed++;
            if (Files.Contains(fileId))
            {
                Status += string.Format("file_{0}{1}", fileId, SPLIT_CHAR);
                return true;
            }
            return false;
        }

        protected void PublishTaskInfo()
        {
            FillDistributedTask();
            TaskInfo.PublishChanges();
        }
    }
}