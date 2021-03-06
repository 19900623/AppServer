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
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security;
using System.ServiceModel.Security;
using System.Text.RegularExpressions;
using System.Web;

using ASC.Api.Collections;
using ASC.Api.Core;
using ASC.Api.Utils;
using ASC.Common;
using ASC.Common.Logging;
using ASC.Common.Utils;
using ASC.Common.Web;
using ASC.Core;
using ASC.Core.Billing;
using ASC.Core.Common.Configuration;
using ASC.Core.Common.Settings;
using ASC.Core.Tenants;
using ASC.Core.Users;
using ASC.Data.Storage;
using ASC.Data.Storage.Configuration;
using ASC.Data.Storage.Migration;
using ASC.FederatedLogin;
using ASC.FederatedLogin.LoginProviders;
using ASC.FederatedLogin.Profile;
using ASC.IPSecurity;
using ASC.MessagingSystem;
using ASC.Security.Cryptography;
using ASC.Web.Api.Models;
using ASC.Web.Api.Routing;
using ASC.Web.Core;
using ASC.Web.Core.Mobile;
using ASC.Web.Core.PublicResources;
using ASC.Web.Core.Sms;
using ASC.Web.Core.Users;
using ASC.Web.Core.Utility;
using ASC.Web.Core.Utility.Settings;
using ASC.Web.Core.WebZones;
using ASC.Web.Core.WhiteLabel;
using ASC.Web.Studio.Core;
using ASC.Web.Studio.Core.Notify;
using ASC.Web.Studio.Core.Quota;
using ASC.Web.Studio.Core.SMS;
using ASC.Web.Studio.Core.Statistic;
using ASC.Web.Studio.Core.TFA;
using ASC.Web.Studio.UserControls.CustomNavigation;
using ASC.Web.Studio.UserControls.FirstTime;
using ASC.Web.Studio.UserControls.Statistics;
using ASC.Web.Studio.Utility;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace ASC.Api.Settings
{
    [DefaultRoute]
    [ApiController]
    public partial class SettingsController : ControllerBase
    {
        //private const int ONE_THREAD = 1;

        //private static readonly DistributedTaskQueue quotaTasks = new DistributedTaskQueue("quotaOperations", ONE_THREAD);
        //private static DistributedTaskQueue LDAPTasks { get; } = new DistributedTaskQueue("ldapOperations");
        //private static DistributedTaskQueue SMTPTasks { get; } = new DistributedTaskQueue("smtpOperations");
        public Tenant Tenant { get { return ApiContext.Tenant; } }
        public ApiContext ApiContext { get; }
        private MessageService MessageService { get; }
        private StudioNotifyService StudioNotifyService { get; }
        private IWebHostEnvironment WebHostEnvironment { get; }
        private IServiceProvider ServiceProvider { get; }
        private EmployeeWraperHelper EmployeeWraperHelper { get; }
        private ConsumerFactory ConsumerFactory { get; }
        private SmsProviderManager SmsProviderManager { get; }
        private TimeZoneConverter TimeZoneConverter { get; }
        private CustomNamingPeople CustomNamingPeople { get; }
        private IPSecurity.IPSecurity IpSecurity { get; }
        private IMemoryCache MemoryCache { get; }
        private ProviderManager ProviderManager { get; }
        private MobileDetector MobileDetector { get; }
        private IOptionsSnapshot<AccountLinker> AccountLinker { get; }
        private FirstTimeTenantSettings FirstTimeTenantSettings { get; }
        private UserManager UserManager { get; }
        private TenantManager TenantManager { get; }
        private TenantExtra TenantExtra { get; }
        private TenantStatisticsProvider TenantStatisticsProvider { get; }
        private AuthContext AuthContext { get; }
        private CookiesManager CookiesManager { get; }
        private WebItemSecurity WebItemSecurity { get; }
        private StudioNotifyHelper StudioNotifyHelper { get; }
        private LicenseReader LicenseReader { get; }
        private PermissionContext PermissionContext { get; }
        private SettingsManager SettingsManager { get; }
        private TfaManager TfaManager { get; }
        private WebItemManager WebItemManager { get; }
        private WebItemManagerSecurity WebItemManagerSecurity { get; }
        private TenantInfoSettingsHelper TenantInfoSettingsHelper { get; }
        private TenantWhiteLabelSettingsHelper TenantWhiteLabelSettingsHelper { get; }
        private StorageHelper StorageHelper { get; }
        private TenantLogoManager TenantLogoManager { get; }
        private TenantUtil TenantUtil { get; }
        private CoreBaseSettings CoreBaseSettings { get; }
        private CommonLinkUtility CommonLinkUtility { get; }
        private ColorThemesSettingsHelper ColorThemesSettingsHelper { get; }
        private IConfiguration Configuration { get; }
        private SetupInfo SetupInfo { get; }
        private BuildVersion BuildVersion { get; }
        private DisplayUserSettingsHelper DisplayUserSettingsHelper { get; }
        private StatisticManager StatisticManager { get; }
        private IPRestrictionsService IPRestrictionsService { get; }
        private CoreConfiguration CoreConfiguration { get; }
        private MessageTarget MessageTarget { get; }
        private StudioSmsNotificationSettingsHelper StudioSmsNotificationSettingsHelper { get; }
        private CoreSettings CoreSettings { get; }
        private StorageSettingsHelper StorageSettingsHelper { get; }
        private ServiceClient ServiceClient { get; }
        public ILog Log { get; set; }

        public SettingsController(
            IOptionsMonitor<ILog> option,
            MessageService messageService,
            StudioNotifyService studioNotifyService,
            ApiContext apiContext,
            UserManager userManager,
            TenantManager tenantManager,
            TenantExtra tenantExtra,
            TenantStatisticsProvider tenantStatisticsProvider,
            AuthContext authContext,
            CookiesManager cookiesManager,
            WebItemSecurity webItemSecurity,
            StudioNotifyHelper studioNotifyHelper,
            LicenseReader licenseReader,
            PermissionContext permissionContext,
            SettingsManager settingsManager,
            TfaManager tfaManager,
            WebItemManager webItemManager,
            WebItemManagerSecurity webItemManagerSecurity,
            TenantInfoSettingsHelper tenantInfoSettingsHelper,
            TenantWhiteLabelSettingsHelper tenantWhiteLabelSettingsHelper,
            StorageHelper storageHelper,
            TenantLogoManager tenantLogoManager,
            TenantUtil tenantUtil,
            CoreBaseSettings coreBaseSettings,
            CommonLinkUtility commonLinkUtility,
            ColorThemesSettingsHelper colorThemesSettingsHelper,
            IConfiguration configuration,
            SetupInfo setupInfo,
            BuildVersion buildVersion,
            DisplayUserSettingsHelper displayUserSettingsHelper,
            StatisticManager statisticManager,
            IPRestrictionsService iPRestrictionsService,
            CoreConfiguration coreConfiguration,
            MessageTarget messageTarget,
            StudioSmsNotificationSettingsHelper studioSmsNotificationSettingsHelper,
            CoreSettings coreSettings,
            StorageSettingsHelper storageSettingsHelper,
            IWebHostEnvironment webHostEnvironment,
            IServiceProvider serviceProvider,
            EmployeeWraperHelper employeeWraperHelper,
            ConsumerFactory consumerFactory,
            SmsProviderManager smsProviderManager,
            TimeZoneConverter timeZoneConverter,
            CustomNamingPeople customNamingPeople,
            IPSecurity.IPSecurity ipSecurity,
            IMemoryCache memoryCache,
            ProviderManager providerManager,
            MobileDetector mobileDetector,
            IOptionsSnapshot<AccountLinker> accountLinker,
            FirstTimeTenantSettings firstTimeTenantSettings,
            ServiceClient serviceClient)
        {
            Log = option.Get("ASC.Api");
            WebHostEnvironment = webHostEnvironment;
            ServiceProvider = serviceProvider;
            EmployeeWraperHelper = employeeWraperHelper;
            ConsumerFactory = consumerFactory;
            SmsProviderManager = smsProviderManager;
            TimeZoneConverter = timeZoneConverter;
            CustomNamingPeople = customNamingPeople;
            IpSecurity = ipSecurity;
            MemoryCache = memoryCache;
            ProviderManager = providerManager;
            MobileDetector = mobileDetector;
            AccountLinker = accountLinker;
            FirstTimeTenantSettings = firstTimeTenantSettings;
            MessageService = messageService;
            StudioNotifyService = studioNotifyService;
            ApiContext = apiContext;
            UserManager = userManager;
            TenantManager = tenantManager;
            TenantExtra = tenantExtra;
            TenantStatisticsProvider = tenantStatisticsProvider;
            AuthContext = authContext;
            CookiesManager = cookiesManager;
            WebItemSecurity = webItemSecurity;
            StudioNotifyHelper = studioNotifyHelper;
            LicenseReader = licenseReader;
            PermissionContext = permissionContext;
            SettingsManager = settingsManager;
            TfaManager = tfaManager;
            WebItemManager = webItemManager;
            WebItemManagerSecurity = webItemManagerSecurity;
            TenantInfoSettingsHelper = tenantInfoSettingsHelper;
            TenantWhiteLabelSettingsHelper = tenantWhiteLabelSettingsHelper;
            StorageHelper = storageHelper;
            TenantLogoManager = tenantLogoManager;
            TenantUtil = tenantUtil;
            CoreBaseSettings = coreBaseSettings;
            CommonLinkUtility = commonLinkUtility;
            ColorThemesSettingsHelper = colorThemesSettingsHelper;
            Configuration = configuration;
            SetupInfo = setupInfo;
            BuildVersion = buildVersion;
            DisplayUserSettingsHelper = displayUserSettingsHelper;
            StatisticManager = statisticManager;
            IPRestrictionsService = iPRestrictionsService;
            CoreConfiguration = coreConfiguration;
            MessageTarget = messageTarget;
            StudioSmsNotificationSettingsHelper = studioSmsNotificationSettingsHelper;
            CoreSettings = coreSettings;
            StorageSettingsHelper = storageSettingsHelper;
            ServiceClient = serviceClient;
        }

        [Read("", Check = false)]
        [AllowAnonymous]
        public SettingsWrapper GetSettings()
        {
            var settings = new SettingsWrapper
            {
                Culture = Tenant.GetCulture().ToString(),
                GreetingSettings = Tenant.Name
            };

            if (AuthContext.IsAuthenticated)
            {
                settings.TrustedDomains = Tenant.TrustedDomains;
                settings.TrustedDomainsType = Tenant.TrustedDomainsType;
                var timeZone = Tenant.TimeZone;
                settings.Timezone = timeZone;
                settings.UtcOffset = TimeZoneConverter.GetTimeZone(timeZone).GetUtcOffset(DateTime.UtcNow);
                settings.UtcHoursOffset = settings.UtcOffset.TotalHours;
                settings.OwnerId = Tenant.OwnerId;
                settings.NameSchemaId = CustomNamingPeople.Current.Id;
            }
            else
            {
                if (!SettingsManager.Load<WizardSettings>().Completed)
                {
                    settings.WizardToken = CommonLinkUtility.GetToken("", ConfirmType.Wizard, userId: Tenant.OwnerId);
                }

                settings.EnabledJoin =
                    (Tenant.TrustedDomainsType == TenantTrustedDomainsType.Custom &&
                    Tenant.TrustedDomains.Count > 0) ||
                    Tenant.TrustedDomainsType == TenantTrustedDomainsType.All;

                var studioAdminMessageSettings = SettingsManager.Load<StudioAdminMessageSettings>();

                settings.EnableAdmMess = studioAdminMessageSettings.Enable || TenantExtra.IsNotPaid();

                settings.ThirdpartyEnable = SetupInfo.ThirdPartyAuthEnabled && ProviderManager.IsNotEmpty;
            }

            return settings;
        }

        [Create("messagesettings")]
        public ContentResult EnableAdminMessageSettings(AdminMessageSettingsModel model)
        {
            PermissionContext.DemandPermissions(SecutiryConstants.EditPortalSettings);

            SettingsManager.Save(new StudioAdminMessageSettings { Enable = model.TurnOn });

            MessageService.Send(MessageAction.AdministratorMessageSettingsUpdated);

            return new ContentResult() { Content = Resource.SuccessfullySaveSettingsMessage };
        }

        [AllowAnonymous]
        [Create("sendadmmail")]
        public ContentResult SendAdmMail(AdminMessageSettingsModel model)
        {
            var studioAdminMessageSettings = SettingsManager.Load<StudioAdminMessageSettings>();
            var enableAdmMess = studioAdminMessageSettings.Enable || TenantExtra.IsNotPaid();

            if (!enableAdmMess)
                throw new MethodAccessException("Method not available");

            if (!model.Email.TestEmailRegex())
                throw new Exception(Resource.ErrorNotCorrectEmail);

            if (string.IsNullOrEmpty(model.Message))
                throw new Exception(Resource.ErrorEmptyMessage);

            CheckCache("sendadmmail");

            StudioNotifyService.SendMsgToAdminFromNotAuthUser(model.Email, model.Message);
            MessageService.Send(MessageAction.ContactAdminMailSent);

            return new ContentResult() { Content = Resource.AdminMessageSent };
        }

        [Create("maildomainsettings")]
        public ContentResult SaveMailDomainSettings(MailDomainSettingsModel model)
        {
            PermissionContext.DemandPermissions(SecutiryConstants.EditPortalSettings);

            if (model.Type == TenantTrustedDomainsType.Custom)
            {
                Tenant.TrustedDomains.Clear();
                foreach (var d in model.Domains.Select(domain => (domain ?? "").Trim().ToLower()))
                {
                    if (!(!string.IsNullOrEmpty(d) && new Regex("^[a-z0-9]([a-z0-9-.]){1,98}[a-z0-9]$").IsMatch(d)))
                        return new ContentResult() { Content = Resource.ErrorNotCorrectTrustedDomain };

                    Tenant.TrustedDomains.Add(d);
                }

                if (Tenant.TrustedDomains.Count == 0)
                    model.Type = TenantTrustedDomainsType.None;
            }

            Tenant.TrustedDomainsType = model.Type;

            SettingsManager.Save(new StudioTrustedDomainSettings { InviteUsersAsVisitors = model.InviteUsersAsVisitors });

            TenantManager.SaveTenant(Tenant);

            MessageService.Send(MessageAction.TrustedMailDomainSettingsUpdated);

            return new ContentResult { Content = Resource.SuccessfullySaveSettingsMessage };
        }

        [AllowAnonymous]
        [Create("sendjoininvite")]
        public ContentResult SendJoinInviteMail(AdminMessageSettingsModel model)
        {
            try
            {
                var email = model.Email;
                if (!(
                    (Tenant.TrustedDomainsType == TenantTrustedDomainsType.Custom &&
                    Tenant.TrustedDomains.Count > 0) ||
                    Tenant.TrustedDomainsType == TenantTrustedDomainsType.All))
                    throw new MethodAccessException("Method not available");

                if (!email.TestEmailRegex())
                    throw new Exception(Resource.ErrorNotCorrectEmail);

                CheckCache("sendjoininvite");

                var user = UserManager.GetUserByEmail(email);
                if (!user.ID.Equals(Constants.LostUser.ID))
                    throw new Exception(CustomNamingPeople.Substitute<Resource>("ErrorEmailAlreadyExists"));

                var settings = SettingsManager.Load<IPRestrictionsSettings>();

                if (settings.Enable && !IpSecurity.Verify())
                    throw new Exception(Resource.ErrorAccessRestricted);

                var trustedDomainSettings = SettingsManager.Load<StudioTrustedDomainSettings>();
                var emplType = trustedDomainSettings.InviteUsersAsVisitors ? EmployeeType.Visitor : EmployeeType.User;
                var enableInviteUsers = TenantStatisticsProvider.GetUsersCount() < TenantExtra.GetTenantQuota().ActiveUsers;

                if (!enableInviteUsers)
                    emplType = EmployeeType.Visitor;

                switch (Tenant.TrustedDomainsType)
                {
                    case TenantTrustedDomainsType.Custom:
                        {
                            var address = new MailAddress(email);
                            if (Tenant.TrustedDomains.Any(d => address.Address.EndsWith("@" + d, StringComparison.InvariantCultureIgnoreCase)))
                            {
                                StudioNotifyService.SendJoinMsg(email, emplType);
                                MessageService.Send(MessageInitiator.System, MessageAction.SentInviteInstructions, email);
                                return new ContentResult() { Content = Resource.FinishInviteJoinEmailMessage };
                            }

                            throw new Exception(Resource.ErrorEmailDomainNotAllowed);
                        }
                    case TenantTrustedDomainsType.All:
                        {
                            StudioNotifyService.SendJoinMsg(email, emplType);
                            MessageService.Send(MessageInitiator.System, MessageAction.SentInviteInstructions, email);
                            return new ContentResult() { Content = Resource.FinishInviteJoinEmailMessage };
                        }
                    default:
                        throw new Exception(Resource.ErrorNotCorrectEmail);
                }
            }
            catch (FormatException)
            {
                return new ContentResult() { Content = Resource.ErrorNotCorrectEmail };
            }
            catch (Exception e)
            {
                return new ContentResult() { Content = e.Message.HtmlEncode() };
            }
        }

        [Read("customschemas")]
        public List<SchemaModel> PeopleSchemas()
        {
            return CustomNamingPeople
                    .GetSchemas()
                    .Select(r =>
                    {
                        var names = CustomNamingPeople.GetPeopleNames(r.Key);
                        var schemaItem = new SchemaItemModel
                        {
                            Id = names.Id,
                            UserCaption = names.UserCaption,
                            UsersCaption = names.UsersCaption,
                            GroupCaption = names.GroupCaption,
                            GroupsCaption = names.GroupsCaption,
                            UserPostCaption = names.UserPostCaption,
                            RegDateCaption = names.RegDateCaption,
                            GroupHeadCaption = names.GroupHeadCaption,
                            GuestCaption = names.GuestCaption,
                            GuestsCaption = names.GuestsCaption,
                        };

                        return new SchemaModel
                        {
                            Id = r.Key,
                            Name = r.Value,
                            Items = schemaItem
                        };
                    })
                    .ToList();
        }

        [Read("customschemas/{id}")]
        public SchemaItemModel PeopleSchema(string id)
        {
            var names = CustomNamingPeople.GetPeopleNames(id);
            var schemaItem = new SchemaItemModel
            {
                Id = names.Id,
                UserCaption = names.UserCaption,
                UsersCaption = names.UsersCaption,
                GroupCaption = names.GroupCaption,
                GroupsCaption = names.GroupsCaption,
                UserPostCaption = names.UserPostCaption,
                RegDateCaption = names.RegDateCaption,
                GroupHeadCaption = names.GroupHeadCaption,
                GuestCaption = names.GuestCaption,
                GuestsCaption = names.GuestsCaption,
            };
            return schemaItem;
        }

        [Read("quota")]
        public QuotaWrapper GetQuotaUsed()
        {
            return new QuotaWrapper(Tenant, CoreBaseSettings, CoreConfiguration, TenantExtra, TenantStatisticsProvider, AuthContext, SettingsManager, WebItemManager);
        }


        [AllowAnonymous]
        [Read("authproviders")]
        public ICollection<AccountInfo> GetAuthProviders(bool inviteView, bool settingsView, string clientCallback, string fromOnly)
        {
            ICollection<AccountInfo> infos = new List<AccountInfo>();
            IEnumerable<LoginProfile> linkedAccounts = new List<LoginProfile>();

            if (AuthContext.IsAuthenticated)
            {
                linkedAccounts = AccountLinker.Get("webstudio").GetLinkedProfiles(AuthContext.CurrentAccount.ID.ToString());
            }

            fromOnly = string.IsNullOrWhiteSpace(fromOnly) ? string.Empty : fromOnly.ToLower();

            foreach (var provider in ProviderManager.AuthProviders.Where(provider => string.IsNullOrEmpty(fromOnly) || fromOnly == provider || (provider == "google" && fromOnly == "openid")))
            {
                if (inviteView && provider.ToLower() == "twitter") continue;

                var loginProvider = ProviderManager.GetLoginProvider(provider);
                if (loginProvider != null && loginProvider.IsEnabled)
                {

                    var url = VirtualPathUtility.ToAbsolute("~/login.ashx") + $"?auth={provider}";
                    var mode = (settingsView || inviteView || (!MobileDetector.IsMobile() && !Request.DesktopApp())
                                     ? ("&mode=popup&callback=" + clientCallback)
                                     : ("&mode=Redirect&returnurl="
                                        + HttpUtility.UrlEncode(new Uri(Request.GetUrlRewriter(),
                                            "Auth.aspx"
                                            + (Request.DesktopApp() ? "?desktop=true" : "")
                                            ).ToString())));

                    infos.Add(new AccountInfo
                    {
                        Linked = linkedAccounts.Any(x => x.Provider == provider),
                        Provider = provider,
                        Url = url + mode
                    });
                }
            }

            return infos;
        }

        [AllowAnonymous]
        [Read("cultures", Check = false)]
        public IEnumerable<object> GetSupportedCultures()
        {
            return SetupInfo.EnabledCultures.Select(r => r.Name).ToArray();
        }

        [Authorize(AuthenticationSchemes = "confirm", Roles = "Wizard,Administrators")]
        [Read("timezones", Check = false)]
        public List<TimezonesModel> GetTimeZones()
        {
            ApiContext.AuthByClaim();
            var timeZones = TimeZoneInfo.GetSystemTimeZones().ToList();

            if (timeZones.All(tz => tz.Id != "UTC"))
            {
                timeZones.Add(TimeZoneInfo.Utc);
            }

            var listOfTimezones = new List<TimezonesModel>();

            foreach (var tz in timeZones.OrderBy(z => z.BaseUtcOffset))
            {
                listOfTimezones.Add(new TimezonesModel
                {
                    Id = tz.Id,
                    DisplayName = TimeZoneConverter.GetTimeZoneDisplayName(tz)
                });
            }

            return listOfTimezones;
        }

        [Authorize(AuthenticationSchemes = "confirm", Roles = "Wizard")]
        [Read("machine", Check = false)]
        public string GetMachineName()
        {
            return Dns.GetHostName().ToLowerInvariant();
        }

        /*        [Read("greetingsettings")]
                public string GetGreetingSettings()
                {
                    return Tenant.Name;
                }*/

        [Create("greetingsettings")]
        public object SaveGreetingSettings(GreetingSettingsModel model)
        {
            try
            {
                PermissionContext.DemandPermissions(SecutiryConstants.EditPortalSettings);

                Tenant.Name = model.Title;
                TenantManager.SaveTenant(Tenant);

                MessageService.Send(MessageAction.GreetingSettingsUpdated);

                return new { Status = 1, Message = Resource.SuccessfullySaveGreetingSettingsMessage };
            }
            catch (Exception e)
            {
                return new { Status = 0, Message = e.Message.HtmlEncode() };
            }
        }

        [Create("greetingsettings/restore")]
        public object RestoreGreetingSettings()
        {
            try
            {
                PermissionContext.DemandPermissions(SecutiryConstants.EditPortalSettings);

                TenantInfoSettingsHelper.RestoreDefaultTenantName();

                return new
                {
                    Status = 1,
                    Message = Resource.SuccessfullySaveGreetingSettingsMessage,
                    CompanyName = Tenant.Name
                };
            }
            catch (Exception e)
            {
                return new { Status = 0, Message = e.Message.HtmlEncode() };
            }
        }

        //[Read("recalculatequota")]
        //public void RecalculateQuota()
        //{
        //    SecurityContext.DemandPermissions(Tenant, SecutiryConstants.EditPortalSettings);

        //    var operations = quotaTasks.GetTasks()
        //        .Where(t => t.GetProperty<int>(QuotaSync.TenantIdKey) == Tenant.TenantId);

        //    if (operations.Any(o => o.Status <= DistributedTaskStatus.Running))
        //    {
        //        throw new InvalidOperationException(Resource.LdapSettingsTooManyOperations);
        //    }

        //    var op = new QuotaSync(Tenant.TenantId, ServiceProvider);

        //    quotaTasks.QueueTask(op.RunJob, op.GetDistributedTask());
        //}

        //[Read("checkrecalculatequota")]
        //public bool CheckRecalculateQuota()
        //{
        //    PermissionContext.DemandPermissions(SecutiryConstants.EditPortalSettings);

        //    var task = quotaTasks.GetTasks().FirstOrDefault(t => t.GetProperty<int>(QuotaSync.TenantIdKey) == Tenant.TenantId);

        //    if (task != null && task.Status == DistributedTaskStatus.Completed)
        //    {
        //        quotaTasks.RemoveTask(task.Id);
        //        return false;
        //    }

        //    return task != null;
        //}

        [AllowAnonymous]
        [Read("version/build", false)]
        public BuildVersion GetBuildVersions()
        {
            return BuildVersion.GetCurrentBuildVersion();
        }

        [Read("version")]
        public TenantVersionWrapper GetVersions()
        {
            return new TenantVersionWrapper(Tenant.Version, TenantManager.GetTenantVersions());
        }

        [Update("version")]
        public TenantVersionWrapper SetVersion(SettingsModel model)
        {
            PermissionContext.DemandPermissions(SecutiryConstants.EditPortalSettings);

            TenantManager.GetTenantVersions().FirstOrDefault(r => r.Id == model.VersionId).NotFoundIfNull();
            TenantManager.SetTenantVersion(Tenant, model.VersionId);

            return GetVersions();
        }

        [Read("security")]
        public IEnumerable<SecurityWrapper> GetWebItemSecurityInfo(IEnumerable<string> ids)
        {
            if (ids == null || !ids.Any())
            {
                ids = WebItemManager.GetItemsAll().Select(i => i.ID.ToString());
            }

            var subItemList = WebItemManager.GetItemsAll().Where(item => item.IsSubItem()).Select(i => i.ID.ToString());

            return ids.Select(r => WebItemSecurity.GetSecurityInfo(r))
                      .Select(i => new SecurityWrapper
                      {
                          WebItemId = i.WebItemId,
                          Enabled = i.Enabled,
                          Users = i.Users.Select(EmployeeWraperHelper.Get),
                          Groups = i.Groups.Select(g => new GroupWrapperSummary(g, UserManager)),
                          IsSubItem = subItemList.Contains(i.WebItemId),
                      }).ToList();
        }

        [Read("security/{id}")]
        public bool GetWebItemSecurityInfo(Guid id)
        {
            var module = WebItemManager[id];

            return module != null && !module.IsDisabled(WebItemSecurity, AuthContext);
        }

        [Read("security/modules")]
        public object GetEnabledModules()
        {
            var EnabledModules = WebItemManagerSecurity.GetItems(WebZoneType.All, ItemAvailableState.Normal)
                                        .Where(item => !item.IsSubItem() && item.Visible)
                                        .ToList()
                                        .Select(item => new
                                        {
                                            id = item.ProductClassName.HtmlEncode(),
                                            title = item.Name.HtmlEncode()
                                        });

            return EnabledModules;
        }

        [Read("security/password", Check = false)]
        [Authorize(AuthenticationSchemes = "confirm", Roles = "Everyone")]
        public object GetPasswordSettings()
        {
            var UserPasswordSettings = SettingsManager.Load<PasswordSettings>();

            return UserPasswordSettings;
        }

        [Update("security")]
        public IEnumerable<SecurityWrapper> SetWebItemSecurity(WebItemSecurityModel model)
        {
            PermissionContext.DemandPermissions(SecutiryConstants.EditPortalSettings);

            WebItemSecurity.SetSecurity(model.Id, model.Enabled, model.Subjects?.ToArray());
            var securityInfo = GetWebItemSecurityInfo(new List<string> { model.Id });

            if (model.Subjects == null) return securityInfo;

            var productName = GetProductName(new Guid(model.Id));

            if (!model.Subjects.Any())
            {
                MessageService.Send(MessageAction.ProductAccessOpened, productName);
            }
            else
            {
                foreach (var info in securityInfo)
                {
                    if (info.Groups.Any())
                    {
                        MessageService.Send(MessageAction.GroupsOpenedProductAccess, productName, info.Groups.Select(x => x.Name));
                    }
                    if (info.Users.Any())
                    {
                        MessageService.Send(MessageAction.UsersOpenedProductAccess, productName, info.Users.Select(x => HttpUtility.HtmlDecode(x.DisplayName)));
                    }
                }
            }

            return securityInfo;
        }

        [Update("security/access")]
        public IEnumerable<SecurityWrapper> SetAccessToWebItems(WebItemSecurityModel model)
        {
            PermissionContext.DemandPermissions(SecutiryConstants.EditPortalSettings);

            var itemList = new ItemDictionary<string, bool>();

            foreach (var item in model.Items)
            {
                if (!itemList.ContainsKey(item.Key))
                    itemList.Add(item.Key, item.Value);
            }

            var defaultPageSettings = SettingsManager.Load<StudioDefaultPageSettings>();

            foreach (var item in itemList)
            {
                Guid[] subjects = null;
                var productId = new Guid(item.Key);

                if (item.Value)
                {
                    if (WebItemManager[productId] is IProduct webItem)
                    {
                        var productInfo = WebItemSecurity.GetSecurityInfo(item.Key);
                        var selectedGroups = productInfo.Groups.Select(group => group.ID).ToList();
                        var selectedUsers = productInfo.Users.Select(user => user.ID).ToList();
                        selectedUsers.AddRange(selectedGroups);
                        if (selectedUsers.Count > 0)
                        {
                            subjects = selectedUsers.ToArray();
                        }
                    }
                }
                else if (productId == defaultPageSettings.DefaultProductID)
                {
                    SettingsManager.Save(defaultPageSettings.GetDefault(ServiceProvider) as StudioDefaultPageSettings);
                }

                WebItemSecurity.SetSecurity(item.Key, item.Value, subjects);
            }

            MessageService.Send(MessageAction.ProductsListUpdated);

            return GetWebItemSecurityInfo(itemList.Keys.ToList());
        }

        [Read("security/administrator/{productid}")]
        public IEnumerable<EmployeeWraper> GetProductAdministrators(Guid productid)
        {
            return WebItemSecurity.GetProductAdministrators(productid)
                                  .Select(EmployeeWraperHelper.Get)
                                  .ToList();
        }

        [Read("security/administrator")]
        public object IsProductAdministrator(Guid productid, Guid userid)
        {
            var result = WebItemSecurity.IsProductAdministrator(productid, userid);
            return new { ProductId = productid, UserId = userid, Administrator = result, };
        }

        [Update("security/administrator")]
        public object SetProductAdministrator(SecurityModel model)
        {
            PermissionContext.DemandPermissions(SecutiryConstants.EditPortalSettings);

            WebItemSecurity.SetProductAdministrator(model.ProductId, model.UserId, model.Administrator);

            var admin = UserManager.GetUsers(model.UserId);

            if (model.ProductId == Guid.Empty)
            {
                var messageAction = model.Administrator ? MessageAction.AdministratorOpenedFullAccess : MessageAction.AdministratorDeleted;
                MessageService.Send(messageAction, MessageTarget.Create(admin.ID), admin.DisplayUserName(false, DisplayUserSettingsHelper));
            }
            else
            {
                var messageAction = model.Administrator ? MessageAction.ProductAddedAdministrator : MessageAction.ProductDeletedAdministrator;
                MessageService.Send(messageAction, MessageTarget.Create(admin.ID), GetProductName(model.ProductId), admin.DisplayUserName(false, DisplayUserSettingsHelper));
            }

            return new { model.ProductId, model.UserId, model.Administrator };
        }

        [Read("logo")]
        public string GetLogo()
        {
            return TenantInfoSettingsHelper.GetAbsoluteCompanyLogoPath(SettingsManager.Load<TenantInfoSettings>());
        }


        ///<visible>false</visible>
        [Create("whitelabel/save")]
        public void SaveWhiteLabelSettings(WhiteLabelModel model)
        {
            PermissionContext.DemandPermissions(SecutiryConstants.EditPortalSettings);

            if (!TenantLogoManager.WhiteLabelEnabled || !TenantLogoManager.WhiteLabelPaid)
            {
                throw new BillingException(Resource.ErrorNotAllowedOption, "WhiteLabel");
            }

            var _tenantWhiteLabelSettings = SettingsManager.Load<TenantWhiteLabelSettings>();

            if (model.Logo != null)
            {
                var logoDict = new Dictionary<int, string>();
                model.Logo.ToList().ForEach(n => logoDict.Add(n.Key, n.Value));

                TenantWhiteLabelSettingsHelper.SetLogo(_tenantWhiteLabelSettings, logoDict);
            }

            _tenantWhiteLabelSettings.SetLogoText(model.LogoText);
            TenantWhiteLabelSettingsHelper.Save(_tenantWhiteLabelSettings, Tenant.TenantId, TenantLogoManager);

        }


        ///<visible>false</visible>
        [Create("whitelabel/savefromfiles")]
        public void SaveWhiteLabelSettingsFromFiles(WhiteLabelModel model)
        {
            if (model.Attachments != null && model.Attachments.Any())
            {
                var _tenantWhiteLabelSettings = SettingsManager.Load<TenantWhiteLabelSettings>();

                foreach (var f in model.Attachments)
                {
                    var parts = f.FileName.Split('.');

                    var logoType = (WhiteLabelLogoTypeEnum)(Convert.ToInt32(parts[0]));
                    var fileExt = parts[1];
                    using var inputStream = f.OpenReadStream();
                    TenantWhiteLabelSettingsHelper.SetLogoFromStream(_tenantWhiteLabelSettings, logoType, fileExt, inputStream);
                }
                TenantWhiteLabelSettingsHelper.Save(_tenantWhiteLabelSettings, Tenant.TenantId, TenantLogoManager);
            }
            else
            {
                throw new InvalidOperationException("No input files");
            }
        }


        ///<visible>false</visible>
        [Read("whitelabel/sizes")]
        public object GetWhiteLabelSizes()
        {
            PermissionContext.DemandPermissions(SecutiryConstants.EditPortalSettings);

            if (!TenantLogoManager.WhiteLabelEnabled)
            {
                throw new BillingException(Resource.ErrorNotAllowedOption, "WhiteLabel");
            }

            return
            new[]
            {
                new {type = (int)WhiteLabelLogoTypeEnum.LightSmall, name = WhiteLabelLogoTypeEnum.LightSmall.ToString(), height = TenantWhiteLabelSettings.logoLightSmallSize.Height, width = TenantWhiteLabelSettings.logoLightSmallSize.Width},
                new {type = (int)WhiteLabelLogoTypeEnum.Dark, name = WhiteLabelLogoTypeEnum.Dark.ToString(), height = TenantWhiteLabelSettings.logoDarkSize.Height, width = TenantWhiteLabelSettings.logoDarkSize.Width},
                new {type = (int)WhiteLabelLogoTypeEnum.Favicon, name = WhiteLabelLogoTypeEnum.Favicon.ToString(), height = TenantWhiteLabelSettings.logoFaviconSize.Height, width = TenantWhiteLabelSettings.logoFaviconSize.Width},
                new {type = (int)WhiteLabelLogoTypeEnum.DocsEditor, name = WhiteLabelLogoTypeEnum.DocsEditor.ToString(), height = TenantWhiteLabelSettings.logoDocsEditorSize.Height, width = TenantWhiteLabelSettings.logoDocsEditorSize.Width}
            };
        }



        ///<visible>false</visible>
        [Read("whitelabel/logos")]
        public Dictionary<int, string> GetWhiteLabelLogos(bool retina)
        {
            PermissionContext.DemandPermissions(SecutiryConstants.EditPortalSettings);

            if (!TenantLogoManager.WhiteLabelEnabled)
            {
                throw new BillingException(Resource.ErrorNotAllowedOption, "WhiteLabel");
            }

            var _tenantWhiteLabelSettings = SettingsManager.Load<TenantWhiteLabelSettings>();


            var result = new Dictionary<int, string>
            {
                { (int)WhiteLabelLogoTypeEnum.LightSmall, CommonLinkUtility.GetFullAbsolutePath(TenantWhiteLabelSettingsHelper.GetAbsoluteLogoPath(_tenantWhiteLabelSettings, WhiteLabelLogoTypeEnum.LightSmall, !retina)) },
                { (int)WhiteLabelLogoTypeEnum.Dark, CommonLinkUtility.GetFullAbsolutePath(TenantWhiteLabelSettingsHelper.GetAbsoluteLogoPath(_tenantWhiteLabelSettings, WhiteLabelLogoTypeEnum.Dark, !retina)) },
                { (int)WhiteLabelLogoTypeEnum.Favicon, CommonLinkUtility.GetFullAbsolutePath(TenantWhiteLabelSettingsHelper.GetAbsoluteLogoPath(_tenantWhiteLabelSettings, WhiteLabelLogoTypeEnum.Favicon, !retina)) },
                { (int)WhiteLabelLogoTypeEnum.DocsEditor, CommonLinkUtility.GetFullAbsolutePath(TenantWhiteLabelSettingsHelper.GetAbsoluteLogoPath(_tenantWhiteLabelSettings, WhiteLabelLogoTypeEnum.DocsEditor, !retina)) }
            };

            return result;
        }

        ///<visible>false</visible>
        [Read("whitelabel/logotext")]
        public string GetWhiteLabelLogoText()
        {
            if (!TenantLogoManager.WhiteLabelEnabled)
            {
                throw new BillingException(Resource.ErrorNotAllowedOption, "WhiteLabel");
            }

            var whiteLabelSettings = SettingsManager.Load<TenantWhiteLabelSettings>();

            return whiteLabelSettings.GetLogoText(SettingsManager) ?? TenantWhiteLabelSettings.DefaultLogoText;
        }


        ///<visible>false</visible>
        [Update("whitelabel/restore")]
        public void RestoreWhiteLabelOptions()
        {
            PermissionContext.DemandPermissions(SecutiryConstants.EditPortalSettings);

            if (!TenantLogoManager.WhiteLabelEnabled || !TenantLogoManager.WhiteLabelPaid)
            {
                throw new BillingException(Resource.ErrorNotAllowedOption, "WhiteLabel");
            }

            var _tenantWhiteLabelSettings = SettingsManager.Load<TenantWhiteLabelSettings>();
            TenantWhiteLabelSettingsHelper.RestoreDefault(_tenantWhiteLabelSettings, TenantLogoManager);

            var _tenantInfoSettings = SettingsManager.Load<TenantInfoSettings>();
            TenantInfoSettingsHelper.RestoreDefaultLogo(_tenantInfoSettings, TenantLogoManager);
            SettingsManager.Save(_tenantInfoSettings);
        }

        [Read("iprestrictions")]
        public IEnumerable<IPRestriction> GetIpRestrictions()
        {
            PermissionContext.DemandPermissions(SecutiryConstants.EditPortalSettings);
            return IPRestrictionsService.Get(Tenant.TenantId);
        }

        [Update("iprestrictions")]
        public IEnumerable<string> SaveIpRestrictions(IpRestrictionsModel model)
        {
            PermissionContext.DemandPermissions(SecutiryConstants.EditPortalSettings);
            return IPRestrictionsService.Save(model.Ips, Tenant.TenantId);
        }

        [Update("iprestrictions/settings")]
        public IPRestrictionsSettings UpdateIpRestrictionsSettings(IpRestrictionsModel model)
        {
            PermissionContext.DemandPermissions(SecutiryConstants.EditPortalSettings);

            var settings = new IPRestrictionsSettings { Enable = model.Enable };
            SettingsManager.Save(settings);

            return settings;
        }

        [Update("tips")]
        public TipsSettings UpdateTipsSettings(SettingsModel model)
        {
            var settings = new TipsSettings { Show = model.Show };
            SettingsManager.SaveForCurrentUser(settings);

            if (!model.Show && !string.IsNullOrEmpty(SetupInfo.TipsAddress))
            {
                try
                {
                    using var client = new WebClient();
                    var data = new NameValueCollection
                    {
                        ["userId"] = AuthContext.CurrentAccount.ID.ToString(),
                        ["tenantId"] = Tenant.TenantId.ToString(CultureInfo.InvariantCulture)
                    };

                    client.UploadValues(string.Format("{0}/tips/deletereaded", SetupInfo.TipsAddress), data);
                }
                catch (Exception e)
                {
                    Log.Error(e.Message, e);
                }
            }

            return settings;
        }

        [Update("tips/change/subscription")]
        public bool UpdateTipsSubscription()
        {
            return StudioPeriodicNotify.ChangeSubscription(AuthContext.CurrentAccount.ID, StudioNotifyHelper);
        }

        [Update("wizard/complete", Check = false)]
        [Authorize(AuthenticationSchemes = "confirm", Roles = "Wizard")]
        public WizardSettings CompleteWizard(WizardModel wizardModel)
        {
            ApiContext.AuthByClaim();

            PermissionContext.DemandPermissions(SecutiryConstants.EditPortalSettings);

            return FirstTimeTenantSettings.SaveData(wizardModel);
        }


        [Update("tfaapp")]
        public bool TfaSettings(TfaModel model)
        {
            PermissionContext.DemandPermissions(SecutiryConstants.EditPortalSettings);

            var result = false;

            MessageAction action;
            var settings = SettingsManager.Load<TfaAppAuthSettings>();

            switch (model.Type)
            {
                case "sms":
                    if (!StudioSmsNotificationSettingsHelper.IsVisibleSettings())
                        throw new Exception(Resource.SmsNotAvailable);

                    if (!SmsProviderManager.Enabled())
                        throw new MethodAccessException();

                    StudioSmsNotificationSettingsHelper.Enable = true;
                    action = MessageAction.TwoFactorAuthenticationEnabledBySms;

                    if (settings.EnableSetting)
                    {
                        settings.EnableSetting = false;
                        SettingsManager.Save(settings);
                    }

                    result = true;

                    break;

                case "app":
                    if (!TfaAppAuthSettings.IsVisibleSettings)
                    {
                        throw new Exception(Resource.TfaAppNotAvailable);
                    }

                    settings.EnableSetting = true;
                    SettingsManager.Save(settings);

                    action = MessageAction.TwoFactorAuthenticationEnabledByTfaApp;

                    if (StudioSmsNotificationSettingsHelper.IsVisibleSettings() && StudioSmsNotificationSettingsHelper.Enable)
                    {
                        StudioSmsNotificationSettingsHelper.Enable = false;
                    }

                    result = true;

                    break;

                default:
                    if (settings.EnableSetting)
                    {
                        settings.EnableSetting = false;
                        SettingsManager.Save(settings);
                    }

                    if (StudioSmsNotificationSettingsHelper.IsVisibleSettings() && StudioSmsNotificationSettingsHelper.Enable)
                    {
                        StudioSmsNotificationSettingsHelper.Enable = false;
                    }

                    action = MessageAction.TwoFactorAuthenticationDisabled;

                    break;
            }

            if (result)
            {
                CookiesManager.ResetTenantCookie();
            }

            MessageService.Send(action);
            return result;
        }

        ///<visible>false</visible>
        [Read("tfaappcodes")]
        public IEnumerable<object> TfaAppGetCodes()
        {
            var currentUser = UserManager.GetUsers(AuthContext.CurrentAccount.ID);

            if (!TfaAppAuthSettings.IsVisibleSettings || !TfaAppUserSettings.EnableForUser(SettingsManager, currentUser.ID))
                throw new Exception(Resource.TfaAppNotAvailable);

            if (currentUser.IsVisitor(UserManager) || currentUser.IsOutsider(UserManager))
                throw new NotSupportedException("Not available.");

            return SettingsManager.LoadForCurrentUser<TfaAppUserSettings>().CodesSetting.Select(r => new { r.IsUsed, r.Code }).ToList();
        }

        [Update("tfaappnewcodes")]
        public IEnumerable<object> TfaAppRequestNewCodes()
        {
            var currentUser = UserManager.GetUsers(AuthContext.CurrentAccount.ID);

            if (!TfaAppAuthSettings.IsVisibleSettings || !TfaAppUserSettings.EnableForUser(SettingsManager, currentUser.ID))
                throw new Exception(Resource.TfaAppNotAvailable);

            if (currentUser.IsVisitor(UserManager) || currentUser.IsOutsider(UserManager))
                throw new NotSupportedException("Not available.");

            var codes = TfaManager.GenerateBackupCodes(currentUser).Select(r => new { r.IsUsed, r.Code }).ToList();
            MessageService.Send(MessageAction.UserConnectedTfaApp, MessageTarget.Create(currentUser.ID), currentUser.DisplayUserName(false, DisplayUserSettingsHelper));
            return codes;
        }

        [Update("tfaappnewapp")]
        public string TfaAppNewApp(TfaModel model)
        {
            var isMe = model.Id.Equals(Guid.Empty);
            var user = UserManager.GetUsers(isMe ? AuthContext.CurrentAccount.ID : model.Id);

            if (!isMe && !PermissionContext.CheckPermissions(new UserSecurityProvider(user.ID), Constants.Action_EditUser))
                throw new SecurityAccessDeniedException(Resource.ErrorAccessDenied);

            if (!TfaAppAuthSettings.IsVisibleSettings || !TfaAppUserSettings.EnableForUser(SettingsManager, user.ID))
                throw new Exception(Resource.TfaAppNotAvailable);

            if (user.IsVisitor(UserManager) || user.IsOutsider(UserManager))
                throw new NotSupportedException("Not available.");

            TfaAppUserSettings.DisableForUser(ServiceProvider, SettingsManager, user.ID);
            MessageService.Send(MessageAction.UserDisconnectedTfaApp, MessageTarget.Create(user.ID), user.DisplayUserName(false, DisplayUserSettingsHelper));

            if (isMe)
            {
                return CommonLinkUtility.GetConfirmationUrl(user.Email, ConfirmType.TfaActivation);
            }

            StudioNotifyService.SendMsgTfaReset(user);
            return string.Empty;
        }

        ///<visible>false</visible>
        [Update("welcome/close")]
        public void CloseWelcomePopup()
        {
            var currentUser = UserManager.GetUsers(AuthContext.CurrentAccount.ID);

            var collaboratorPopupSettings = SettingsManager.LoadForCurrentUser<CollaboratorSettings>();

            if (!(currentUser.IsVisitor(UserManager) && collaboratorPopupSettings.FirstVisit && !currentUser.IsOutsider(UserManager)))
                throw new NotSupportedException("Not available.");

            collaboratorPopupSettings.FirstVisit = false;
            SettingsManager.SaveForCurrentUser(collaboratorPopupSettings);
        }

        ///<visible>false</visible>
        [Update("colortheme")]
        public void SaveColorTheme(SettingsModel model)
        {
            PermissionContext.DemandPermissions(SecutiryConstants.EditPortalSettings);
            ColorThemesSettingsHelper.SaveColorTheme(model.Theme);
            MessageService.Send(MessageAction.ColorThemeChanged);
        }

        ///<visible>false</visible>
        [Update("timeandlanguage")]
        public string TimaAndLanguage(SettingsModel model)
        {
            PermissionContext.DemandPermissions(SecutiryConstants.EditPortalSettings);

            var culture = CultureInfo.GetCultureInfo(model.Lng);

            var changelng = false;
            if (SetupInfo.EnabledCultures.Find(c => string.Equals(c.Name, culture.Name, StringComparison.InvariantCultureIgnoreCase)) != null)
            {
                if (!string.Equals(Tenant.Language, culture.Name, StringComparison.InvariantCultureIgnoreCase))
                {
                    Tenant.Language = culture.Name;
                    changelng = true;
                }
            }

            var oldTimeZone = Tenant.TimeZone;
            var timeZones = TimeZoneInfo.GetSystemTimeZones().ToList();
            if (timeZones.All(tz => tz.Id != "UTC"))
            {
                timeZones.Add(TimeZoneInfo.Utc);
            }
            Tenant.TimeZone = timeZones.FirstOrDefault(tz => tz.Id == model.TimeZoneID)?.Id ?? TimeZoneInfo.Utc.Id;

            TenantManager.SaveTenant(Tenant);

            if (!Tenant.TimeZone.Equals(oldTimeZone) || changelng)
            {
                if (!Tenant.TimeZone.Equals(oldTimeZone))
                {
                    MessageService.Send(MessageAction.TimeZoneSettingsUpdated);
                }
                if (changelng)
                {
                    MessageService.Send(MessageAction.LanguageSettingsUpdated);
                }
            }

            return Resource.SuccessfullySaveSettingsMessage;
        }

        [Create("owner")]
        public object SendOwnerChangeInstructions(SettingsModel model)
        {
            PermissionContext.DemandPermissions(SecutiryConstants.EditPortalSettings);

            var curTenant = TenantManager.GetCurrentTenant();
            var owner = UserManager.GetUsers(curTenant.OwnerId);
            var newOwner = UserManager.GetUsers(model.OwnerId);

            if (newOwner.IsVisitor(UserManager)) throw new System.Security.SecurityException("Collaborator can not be an owner");

            if (!owner.ID.Equals(AuthContext.CurrentAccount.ID) || Guid.Empty.Equals(newOwner.ID))
            {
                return new { Status = 0, Message = Resource.ErrorAccessDenied };
            }

            var confirmLink = CommonLinkUtility.GetConfirmationUrl(owner.Email, ConfirmType.PortalOwnerChange, newOwner.ID, newOwner.ID);
            StudioNotifyService.SendMsgConfirmChangeOwner(owner, newOwner, confirmLink);

            MessageService.Send(MessageAction.OwnerSentChangeOwnerInstructions, MessageTarget.Create(owner.ID), owner.DisplayUserName(false, DisplayUserSettingsHelper));

            var emailLink = string.Format("<a href=\"mailto:{0}\">{0}</a>", owner.Email);
            return new { Status = 1, Message = Resource.ChangePortalOwnerMsg.Replace(":email", emailLink) };
        }

        [Update("owner")]
        [Authorize(AuthenticationSchemes = "confirm", Roles = "PortalOwnerChange")]
        public void Owner(SettingsModel model)
        {
            var newOwner = Constants.LostUser;
            try
            {
                newOwner = UserManager.GetUsers(model.OwnerId);
            }
            catch
            {
            }
            if (Constants.LostUser.Equals(newOwner))
            {
                throw new Exception(Resource.ErrorUserNotFound);
            }

            if (UserManager.IsUserInGroup(newOwner.ID, Constants.GroupVisitor.ID))
            {
                throw new Exception(Resource.ErrorUserNotFound);
            }

            var curTenant = TenantManager.GetCurrentTenant();
            curTenant.OwnerId = newOwner.ID;
            TenantManager.SaveTenant(curTenant);

            MessageService.Send(MessageAction.OwnerUpdated, newOwner.DisplayUserName(false, DisplayUserSettingsHelper));
        }

        ///<visible>false</visible>
        [Update("defaultpage")]
        public string SaveDefaultPageSettings(SettingsModel model)
        {
            PermissionContext.DemandPermissions(SecutiryConstants.EditPortalSettings);

            SettingsManager.Save(new StudioDefaultPageSettings { DefaultProductID = model.DefaultProductID });

            MessageService.Send(MessageAction.DefaultStartPageSettingsUpdated);

            return Resource.SuccessfullySaveSettingsMessage;
        }


        private string GetProductName(Guid productId)
        {
            var product = WebItemManager[productId];
            return productId == Guid.Empty ? "All" : product != null ? product.Name : productId.ToString();
        }

        [Read("license/refresh", Check = false)]
        public bool RefreshLicense()
        {
            if (!CoreBaseSettings.Standalone) return false;
            LicenseReader.RefreshLicense();
            return true;
        }

        [AllowAnonymous]
        [Read("license/required", Check = false)]
        public bool RequestLicense()
        {
            return FirstTimeTenantSettings.RequestLicense;
        }


        [Create("license", Check = false)]
        [Authorize(AuthenticationSchemes = "confirm", Roles = "Wizard")]
        public object UploadLicense([FromForm] UploadLicenseModel model)
        {
            try
            {
                if (!AuthContext.IsAuthenticated && SettingsManager.Load<WizardSettings>().Completed) throw new SecurityException(Resource.PortalSecurity);
                if (!model.Files.Any()) throw new Exception(Resource.ErrorEmptyUploadFileSelected);

                ApiContext.AuthByClaim();

                var licenseFile = model.Files.First();
                var dueDate = LicenseReader.SaveLicenseTemp(licenseFile.OpenReadStream());

                return dueDate >= DateTime.UtcNow.Date
                                     ? Resource.LicenseUploaded
                                     : string.Format(Resource.LicenseUploadedOverdue,
                                                     "",
                                                     "",
                                                     dueDate.Date.ToLongDateString());
            }
            catch (LicenseExpiredException ex)
            {
                Log.Error("License upload", ex);
                throw new Exception(Resource.LicenseErrorExpired);
            }
            catch (LicenseQuotaException ex)
            {
                Log.Error("License upload", ex);
                throw new Exception(Resource.LicenseErrorQuota);
            }
            catch (LicensePortalException ex)
            {
                Log.Error("License upload", ex);
                throw new Exception(Resource.LicenseErrorPortal);
            }
            catch (Exception ex)
            {
                Log.Error("License upload", ex);
                throw new Exception(Resource.LicenseError);
            }
        }


        [Read("customnavigation/getall")]
        public List<CustomNavigationItem> GetCustomNavigationItems()
        {
            return SettingsManager.Load<CustomNavigationSettings>().Items;
        }

        [Read("customnavigation/getsample")]
        public CustomNavigationItem GetCustomNavigationItemSample()
        {
            return CustomNavigationItem.GetSample();
        }

        [Read("customnavigation/get/{id}")]
        public CustomNavigationItem GetCustomNavigationItem(Guid id)
        {
            return SettingsManager.Load<CustomNavigationSettings>().Items.FirstOrDefault(item => item.Id == id);
        }

        [Create("customnavigation/create")]
        public CustomNavigationItem CreateCustomNavigationItem(CustomNavigationItem item)
        {
            PermissionContext.DemandPermissions(SecutiryConstants.EditPortalSettings);

            var settings = SettingsManager.Load<CustomNavigationSettings>();

            var exist = false;

            foreach (var existItem in settings.Items)
            {
                if (existItem.Id != item.Id) continue;

                existItem.Label = item.Label;
                existItem.Url = item.Url;
                existItem.ShowInMenu = item.ShowInMenu;
                existItem.ShowOnHomePage = item.ShowOnHomePage;

                if (existItem.SmallImg != item.SmallImg)
                {
                    StorageHelper.DeleteLogo(existItem.SmallImg);
                    existItem.SmallImg = StorageHelper.SaveTmpLogo(item.SmallImg);
                }

                if (existItem.BigImg != item.BigImg)
                {
                    StorageHelper.DeleteLogo(existItem.BigImg);
                    existItem.BigImg = StorageHelper.SaveTmpLogo(item.BigImg);
                }

                exist = true;
                break;
            }

            if (!exist)
            {
                item.Id = Guid.NewGuid();
                item.SmallImg = StorageHelper.SaveTmpLogo(item.SmallImg);
                item.BigImg = StorageHelper.SaveTmpLogo(item.BigImg);

                settings.Items.Add(item);
            }

            SettingsManager.Save(settings);

            MessageService.Send(MessageAction.CustomNavigationSettingsUpdated);

            return item;
        }

        [Delete("customnavigation/delete/{id}")]
        public void DeleteCustomNavigationItem(Guid id)
        {
            PermissionContext.DemandPermissions(SecutiryConstants.EditPortalSettings);

            var settings = SettingsManager.Load<CustomNavigationSettings>();

            var terget = settings.Items.FirstOrDefault(item => item.Id == id);

            if (terget == null) return;

            StorageHelper.DeleteLogo(terget.SmallImg);
            StorageHelper.DeleteLogo(terget.BigImg);

            settings.Items.Remove(terget);
            SettingsManager.Save(settings);

            MessageService.Send(MessageAction.CustomNavigationSettingsUpdated);
        }

        [Update("emailactivation")]
        public EmailActivationSettings UpdateEmailActivationSettings(bool show)
        {
            var settings = new EmailActivationSettings { Show = show };

            SettingsManager.SaveForCurrentUser(settings);

            return settings;
        }

        ///<visible>false</visible>
        [Read("companywhitelabel")]
        public List<CompanyWhiteLabelSettings> GetCompanyWhiteLabelSettings()
        {
            var result = new List<CompanyWhiteLabelSettings>();

            var instance = CompanyWhiteLabelSettings.Instance(SettingsManager);

            result.Add(instance);

            if (!instance.IsDefault(CoreSettings) && !instance.IsLicensor)
            {
                result.Add(instance.GetDefault(ServiceProvider) as CompanyWhiteLabelSettings);
            }

            return result;
        }

        [Read("statistics/spaceusage/{id}")]
        public List<UsageSpaceStatItemWrapper> GetSpaceUsageStatistics(Guid id)
        {
            PermissionContext.DemandPermissions(SecutiryConstants.EditPortalSettings);

            var webtem = WebItemManagerSecurity.GetItems(WebZoneType.All, ItemAvailableState.All)
                                       .FirstOrDefault(item =>
                                                       item != null &&
                                                       item.ID == id &&
                                                       item.Context != null &&
                                                       item.Context.SpaceUsageStatManager != null);

            if (webtem == null) return new List<UsageSpaceStatItemWrapper>();

            return webtem.Context.SpaceUsageStatManager.GetStatData()
                         .ConvertAll(it => new UsageSpaceStatItemWrapper
                         {
                             Name = it.Name.HtmlEncode(),
                             Icon = it.ImgUrl,
                             Disabled = it.Disabled,
                             Size = FileSizeComment.FilesSizeToString(it.SpaceUsage),
                             Url = it.Url
                         });
        }

        [Read("statistics/visit")]
        public List<ChartPointWrapper> GetVisitStatistics(ApiDateTime fromDate, ApiDateTime toDate)
        {
            PermissionContext.DemandPermissions(SecutiryConstants.EditPortalSettings);

            var from = TenantUtil.DateTimeFromUtc(fromDate);
            var to = TenantUtil.DateTimeFromUtc(toDate);

            var points = new List<ChartPointWrapper>();

            if (from.CompareTo(to) >= 0) return points;

            for (var d = new DateTime(from.Ticks); d.Date.CompareTo(to.Date) <= 0; d = d.AddDays(1))
            {
                points.Add(new ChartPointWrapper
                {
                    DisplayDate = d.Date.ToShortDateString(),
                    Date = d.Date,
                    Hosts = 0,
                    Hits = 0
                });
            }

            var hits = StatisticManager.GetHitsByPeriod(Tenant.TenantId, from, to);
            var hosts = StatisticManager.GetHostsByPeriod(Tenant.TenantId, from, to);

            if (hits.Count == 0 || hosts.Count == 0) return points;

            hits.Sort((x, y) => x.VisitDate.CompareTo(y.VisitDate));
            hosts.Sort((x, y) => x.VisitDate.CompareTo(y.VisitDate));

            for (int i = 0, n = points.Count, hitsNum = 0, hostsNum = 0; i < n; i++)
            {
                while (hitsNum < hits.Count && points[i].Date.CompareTo(hits[hitsNum].VisitDate.Date) == 0)
                {
                    points[i].Hits += hits[hitsNum].VisitCount;
                    hitsNum++;
                }
                while (hostsNum < hosts.Count && points[i].Date.CompareTo(hosts[hostsNum].VisitDate.Date) == 0)
                {
                    points[i].Hosts++;
                    hostsNum++;
                }
            }

            return points;
        }

        [Read("storage")]
        public List<StorageWrapper> GetAllStorages()
        {
            PermissionContext.DemandPermissions(SecutiryConstants.EditPortalSettings);

            var current = SettingsManager.Load<StorageSettings>();
            var consumers = ConsumerFactory.GetAll<DataStoreConsumer>().ToList();
            return consumers.Select(consumer => new StorageWrapper(consumer, current)).ToList();
        }

        [Read("storage/progress", false)]
        public double GetStorageProgress()
        {
            PermissionContext.DemandPermissions(SecutiryConstants.EditPortalSettings);

            if (!CoreBaseSettings.Standalone) return -1;

            return ServiceClient.GetProgress(Tenant.TenantId);
        }

        [Update("storage")]
        public StorageSettings UpdateStorage(StorageModel model)
        {
            try
            {
                PermissionContext.DemandPermissions(SecutiryConstants.EditPortalSettings);
                if (!CoreBaseSettings.Standalone) return null;

                var consumer = ConsumerFactory.GetByKey(model.Module);
                if (!consumer.IsSet)
                    throw new ArgumentException("module");

                var settings = SettingsManager.Load<StorageSettings>();
                if (settings.Module == model.Module) return settings;

                settings.Module = model.Module;
                settings.Props = model.Props.ToDictionary(r => r.Key, b => b.Value);

                StartMigrate(settings);
                return settings;
            }
            catch (Exception e)
            {
                Log.Error("UpdateStorage", e);
                throw;
            }
        }

        [Delete("storage")]
        public void ResetStorageToDefault()
        {
            try
            {
                PermissionContext.DemandPermissions(SecutiryConstants.EditPortalSettings);
                if (!CoreBaseSettings.Standalone) return;

                var settings = SettingsManager.Load<StorageSettings>();

                settings.Module = null;
                settings.Props = null;


                StartMigrate(settings);
            }
            catch (Exception e)
            {
                Log.Error("ResetStorageToDefault", e);
                throw;
            }
        }

        [Read("storage/cdn")]
        public List<StorageWrapper> GetAllCdnStorages()
        {
            PermissionContext.DemandPermissions(SecutiryConstants.EditPortalSettings);
            if (!CoreBaseSettings.Standalone) return null;

            var current = SettingsManager.Load<CdnStorageSettings>();
            var consumers = ConsumerFactory.GetAll<DataStoreConsumer>().Where(r => r.Cdn != null).ToList();
            return consumers.Select(consumer => new StorageWrapper(consumer, current)).ToList();
        }

        [Update("storage/cdn")]
        public CdnStorageSettings UpdateCdn(StorageModel model)
        {
            PermissionContext.DemandPermissions(SecutiryConstants.EditPortalSettings);
            if (!CoreBaseSettings.Standalone) return null;

            var consumer = ConsumerFactory.GetByKey(model.Module);
            if (!consumer.IsSet)
                throw new ArgumentException("module");

            var settings = SettingsManager.Load<CdnStorageSettings>();
            if (settings.Module == model.Module) return settings;

            settings.Module = model.Module;
            settings.Props = model.Props.ToDictionary(r => r.Key, b => b.Value);

            try
            {
                ServiceClient.UploadCdn(Tenant.TenantId, "/", WebHostEnvironment.ContentRootPath, settings);
            }
            catch (Exception e)
            {
                Log.Error("UpdateCdn", e);
                throw;
            }

            return settings;
        }

        [Delete("storage/cdn")]
        public void ResetCdnToDefault()
        {
            PermissionContext.DemandPermissions(SecutiryConstants.EditPortalSettings);
            if (!CoreBaseSettings.Standalone) return;

            StorageSettingsHelper.Clear(SettingsManager.Load<CdnStorageSettings>());
        }

        //[Read("storage/backup")]
        //public List<StorageWrapper> GetAllBackupStorages()
        //{
        //    PermissionContext.DemandPermissions(Tenant, SecutiryConstants.EditPortalSettings);

        //    var schedule = new BackupAjaxHandler().GetSchedule();
        //    var current = new StorageSettings();

        //    if (schedule != null && schedule.StorageType == Contracts.BackupStorageType.ThirdPartyConsumer)
        //    {
        //        current = new StorageSettings
        //        {
        //            Module = schedule.StorageParams["module"],
        //            Props = schedule.StorageParams.Where(r => r.Key != "module").ToDictionary(r => r.Key, r => r.Value)
        //        };
        //    }

        //    var consumers = ConsumerFactory.GetAll<DataStoreConsumer>().ToList();
        //    return consumers.Select(consumer => new StorageWrapper(consumer, current)).ToList();
        //}

        private void StartMigrate(StorageSettings settings)
        {
            ServiceClient.Migrate(Tenant.TenantId, settings);

            Tenant.SetStatus(TenantStatus.Migrating);
            TenantManager.SaveTenant(Tenant);
        }

        [Read("socket")]
        public object GetSocketSettings()
        {
            var hubUrl = Configuration["web:hub"] ?? string.Empty;
            if (hubUrl != string.Empty)
            {
                if (!hubUrl.EndsWith("/"))
                {
                    hubUrl += "/";
                }
            }

            return new { Url = hubUrl };
        }


        [Read("authservice")]
        public IEnumerable<AuthServiceModel> GetAuthServices()
        {
            return ConsumerFactory.GetAll<Consumer>()
                .Where(consumer => consumer.ManagedKeys.Any())
                .OrderBy(services => services.Order)
                .Select(r => new AuthServiceModel(r))
                .ToList();
        }

        [Create("authservice")]
        public bool SaveAuthKeys(AuthServiceModel model)
        {
            PermissionContext.DemandPermissions(SecutiryConstants.EditPortalSettings);
            if (!SetupInfo.IsVisibleSettings(ManagementType.ThirdPartyAuthorization.ToString()))
                throw new BillingException(Resource.ErrorNotAllowedOption, "ThirdPartyAuthorization");

            var changed = false;
            var consumer = ConsumerFactory.GetByKey<Consumer>(model.Name);

            var validateKeyProvider = (IValidateKeysProvider)ConsumerFactory.GetAll<Consumer>().FirstOrDefault(r => r.Name == consumer.Name && r is IValidateKeysProvider);
            if (validateKeyProvider != null)
            {
                try
                {
                    if (validateKeyProvider is TwilioProvider twilioLoginProvider)
                    {
                        twilioLoginProvider.ClearOldNumbers();
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }

            if (model.Props.All(r => string.IsNullOrEmpty(r.Value)))
            {
                consumer.Clear();
                changed = true;
            }
            else
            {
                foreach (var authKey in model.Props.Where(authKey => consumer[authKey.Name] != authKey.Value))
                {
                    consumer[authKey.Name] = authKey.Value;
                    changed = true;
                }
            }

            if (validateKeyProvider != null && !validateKeyProvider.ValidateKeys() && !consumer.All(r => string.IsNullOrEmpty(r.Value)))
            {
                consumer.Clear();
                throw new ArgumentException(Resource.ErrorBadKeys);
            }

            if (changed)
                MessageService.Send(MessageAction.AuthorizationKeysSetting);

            return changed;
        }

        [Read("payment")]
        public object PaymentSettings()
        {
            var settings = SettingsManager.LoadForDefaultTenant<AdditionalWhiteLabelSettings>();
            var currentQuota = TenantExtra.GetTenantQuota();
            var currentTariff = TenantExtra.GetCurrentTariff();

            return
                new
                {
                    settings.SalesEmail,
                    settings.FeedbackAndSupportUrl,
                    settings.BuyUrl,
                    CoreBaseSettings.Standalone,
                    currentLicense = new
                    {
                        currentQuota.Trial,
                        currentTariff.DueDate.Date
                    }
                };
        }

        private readonly int maxCount = 10;
        private readonly int expirationMinutes = 2;
        private void CheckCache(string basekey)
        {
            var key = ApiContext.HttpContextAccessor.HttpContext.Request.GetUserHostAddress() + basekey;
            if (MemoryCache.TryGetValue<int>(key, out var count))
            {
                if (count > maxCount)
                    throw new Exception(Resource.ErrorRequestLimitExceeded);
            }

            MemoryCache.Set(key, ++count, TimeSpan.FromMinutes(expirationMinutes));
        }
    }

    public static class SettingsControllerExtension
    {
        public static DIHelper AddSettingsController(this DIHelper services)
        {
            return services
                .AddMessageTargetService()
                .AddCoreConfigurationService()
                .AddIPRestrictionsService()
                .AddDisplayUserSettingsService()
                .AddSetupInfo()
                .AddCommonLinkUtilityService()
                .AddCoreBaseSettingsService()
                .AddTenantUtilService()
                .AddEmailValidationKeyProviderService()
                .AddMessageServiceService()
                .AddStudioNotifyServiceService()
                .AddApiContextService()
                .AddUserManagerService()
                .AddTenantManagerService()
                .AddTenantExtraService()
                .AddTenantStatisticsProviderService()
                .AddUserPhotoManagerService()
                .AddAuthContextService()
                .AddCookiesManagerService()
                .AddWebItemSecurity()
                .AddStudioNotifyHelperService()
                .AddLicenseReaderService()
                .AddPermissionContextService()
                .AddWebItemManager()
                .AddWebItemManagerSecurity()
                .AddCdnStorageSettingsService()
                .AddStorageSettingsService()
                .AddStorageFactoryService()
                .AddStorageFactoryConfigService()
                .AddSettingsManagerService()
                .AddTenantInfoSettingsService()
                .AddColorThemesSettingsHelperService()
                .AddTenantWhiteLabelSettingsService()
                .AddStudioSmsNotificationSettingsService()
                .AddTfaManagerService()
                .AddStorageHelperService()
                .AddTenantLogoManagerService()
                .AddBuildVersionService()
                .AddStatisticManagerService()
                .AddEmployeeWraper()
                .AddConsumerFactoryService()
                .AddSmsProviderManagerService()
                .AddCustomNamingPeopleService()
                .AddProviderManagerService()
                .AddAccountLinker()
                .AddMobileDetectorService()
                .AddFirstTimeTenantSettings()
                .AddServiceClient()
                .AddTwilioProviderService();
        }
    }
}