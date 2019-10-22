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
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ASC.Common.Caching;
using ASC.Common.Logging;
using ASC.Common.Threading;
using ASC.Common.Threading.Progress;
using ASC.Core;
using ASC.Core.Tenants;
using ASC.Data.Storage.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ASC.Data.Storage
{
    public class StorageUploader
    {
        private static readonly TaskScheduler Scheduler;
        private static readonly CancellationTokenSource TokenSource;

        private static readonly ICache Cache;
        private static readonly object Locker;

        public IServiceProvider ServiceProvider { get; }

        static StorageUploader()
        {
            Scheduler = new LimitedConcurrencyLevelTaskScheduler(4);
            TokenSource = new CancellationTokenSource();
            Cache = AscCache.Memory;
            Locker = new object();
        }

        public StorageUploader(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        public void Start(int tenantId, StorageSettings newStorageSettings, StorageFactoryConfig storageFactoryConfig)
        {
            if (TokenSource.Token.IsCancellationRequested) return;

            MigrateOperation migrateOperation;

            lock (Locker)
            {
                migrateOperation = Cache.Get<MigrateOperation>(GetCacheKey(tenantId));
                if (migrateOperation != null) return;

                migrateOperation = new MigrateOperation(ServiceProvider, tenantId, newStorageSettings, storageFactoryConfig);
                Cache.Insert(GetCacheKey(tenantId), migrateOperation, DateTime.MaxValue);
            }

            var task = new Task(migrateOperation.RunJob, TokenSource.Token, TaskCreationOptions.LongRunning);

            task.ConfigureAwait(false)
                .GetAwaiter()
                .OnCompleted(() =>
                {
                    lock (Locker)
                    {
                        Cache.Remove(GetCacheKey(tenantId));
                    }
                });

            task.Start(Scheduler);
        }

        public static MigrateOperation GetProgress(int tenantId)
        {
            lock (Locker)
            {
                return Cache.Get<MigrateOperation>(GetCacheKey(tenantId));
            }
        }

        public static void Stop()
        {
            TokenSource.Cancel();
        }

        private static string GetCacheKey(int tenantId)
        {
            return typeof(MigrateOperation).FullName + tenantId;
        }
    }

    [DataContract]
    public class MigrateOperation : ProgressBase
    {
        private readonly ILog Log;
        private static readonly string ConfigPath;
        private readonly IEnumerable<string> Modules;
        private readonly StorageSettings settings;
        private readonly int tenantId;

        static MigrateOperation()
        {
            ConfigPath = "";
        }

        public MigrateOperation(IServiceProvider serviceProvider, int tenantId, StorageSettings settings, StorageFactoryConfig storageFactoryConfig)
        {
            ServiceProvider = serviceProvider;
            this.tenantId = tenantId;
            this.settings = settings;
            StorageFactoryConfig = storageFactoryConfig;
            Modules = storageFactoryConfig.GetModuleList(ConfigPath, true);
            StepCount = Modules.Count();
            Log = serviceProvider.GetService<IOptionsMonitor<LogNLog>>().Get("ASC");
        }

        public IServiceProvider ServiceProvider { get; }
        public StorageFactoryConfig StorageFactoryConfig { get; }

        protected override void DoJob()
        {
            try
            {
                Log.DebugFormat("Tenant: {0}", tenantId);
                using var scope = ServiceProvider.CreateScope();
                var tenantManager = scope.ServiceProvider.GetService<TenantManager>();
                var tenant = tenantManager.GetTenant(tenantId);
                tenantManager.SetCurrentTenant(tenant);

                var SecurityContext = scope.ServiceProvider.GetService<SecurityContext>();
                var storageFactory = scope.ServiceProvider.GetService<StorageFactory>();
                var options = scope.ServiceProvider.GetService<IOptionsMonitor<LogNLog>>();

                SecurityContext.AuthenticateMe(tenant.OwnerId);

                foreach (var module in Modules)
                {
                    var oldStore = storageFactory.GetStorage(ConfigPath, tenantId.ToString(), module);
                    var store = storageFactory.GetStorageFromConsumer(ConfigPath, tenantId.ToString(), module, settings.DataStoreConsumer);
                    var domains = StorageFactoryConfig.GetDomainList(ConfigPath, module).ToList();

                    var crossModuleTransferUtility = new CrossModuleTransferUtility(options, oldStore, store);

                    string[] files;
                    foreach (var domain in domains)
                    {
                        Status = module + domain;
                        Log.DebugFormat("Domain: {0}", domain);
                        files = oldStore.ListFilesRelative(domain, "\\", "*.*", true);

                        foreach (var file in files)
                        {
                            Log.DebugFormat("File: {0}", file);
                            crossModuleTransferUtility.CopyFile(domain, file, domain, file);
                        }
                    }

                    Log.Debug("Domain:");

                    files = oldStore.ListFilesRelative(string.Empty, "\\", "*.*", true)
                        .Where(path => domains.All(domain => !path.Contains(domain + "/")))
                        .ToArray();

                    foreach (var file in files)
                    {
                        Log.DebugFormat("File: {0}", file);
                        crossModuleTransferUtility.CopyFile("", file, "", file);
                    }

                    StepDone();
                }

                settings.Save();
                tenant.SetStatus(TenantStatus.Active);
                tenantManager.SaveTenant(tenant);
            }
            catch (Exception e)
            {
                Error = e;
                Log.Error(e);
            }
        }
    }
}
