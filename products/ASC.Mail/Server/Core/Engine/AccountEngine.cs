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
using System.Net.Mail;
using System.Threading;
using ASC.Api.Core;
//using System.Net.Mail;
//using System.Threading;
using ASC.Common.Logging;
using ASC.Core;
using ASC.Core.Common.EF;
using ASC.Mail.Clients;
//using ASC.FederatedLogin.Helpers;
//using ASC.FederatedLogin.LoginProviders;
//using ASC.Mail.Authorization;
//using ASC.Mail.Clients;
//using ASC.Mail.Core.Dao.Expressions.Mailbox;
//using ASC.Mail.Core.Entities;
using ASC.Mail.Core.Dao;
using ASC.Mail.Enums;
using ASC.Mail.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
//using ASC.Mail.Utils;
using Microsoft.Extensions.Options;

namespace ASC.Mail.Core.Engine
{
    public class AccountEngine
    {
        public int Tenant
        {
            get
            {
                return ApiContext.Tenant.TenantId;
            }
        }

        public string UserId
        {
            get
            {
                return SecurityContext.CurrentAccount.ID.ToString();
            }
        }

        public SecurityContext SecurityContext { get; }

        public ApiContext ApiContext { get; }

        public ILog Log { get; }

        public MailboxEngine MailboxEngine { get; }
        public DaoFactory DaoFactory { get; }
        public MailDbContext MailDb { get; }


        public AccountEngine(
            ApiContext apiContext,
            SecurityContext securityContext,
            MailboxEngine mailboxEngine,
            IOptionsMonitor<ILog> option,
            DaoFactory daoFactory)
        {
            ApiContext = apiContext;
            SecurityContext = securityContext;
            Log = option.Get("ASC.Mail.AccountEngine");

            MailboxEngine = mailboxEngine;
            DaoFactory = daoFactory;
        }

        public List<AccountInfo> GetAccountInfoList()
        {
            //TODO: Find better solution for cache
            //var accountInfoList = CacheEngine.Get(User);
            //if (accountInfoList != null)
            //    return accountInfoList;

            var accounts = DaoFactory.AccountDao.GetAccounts();

            var accountInfoList = new List<AccountInfo>();

            foreach (var account in accounts)
            {
                var mailboxId = account.MailboxId;
                var accountIndex = accountInfoList.FindIndex(a => a.Id == mailboxId);

                var isAlias = account.ServerAddressIsAlias;

                if (!isAlias)
                {
                    var groupAddress = account.ServerMailGroupAddress;
                    MailAddressInfo group = null;

                    if (!string.IsNullOrEmpty(groupAddress))
                    {
                        group = new MailAddressInfo(account.ServerMailGroupId,
                            groupAddress,
                            account.ServerDomainId);
                    }

                    if (accountIndex == -1)
                    {
                        var authErrorType = MailBoxData.AuthProblemType.NoProblems;

                        if (account.MailboxDateAuthError.HasValue)
                        {
                            var authErrorDate = account.MailboxDateAuthError.Value;

                            if (DateTime.UtcNow - authErrorDate > Defines.AuthErrorDisableTimeout)
                                authErrorType = MailBoxData.AuthProblemType.TooManyErrors;
                            else if (DateTime.UtcNow - authErrorDate > Defines.AuthErrorWarningTimeout)
                                authErrorType = MailBoxData.AuthProblemType.ConnectError;
                        }

                        var accountInfo = new AccountInfo(
                            mailboxId,
                            account.MailboxAddress,
                            account.MailboxAddressName,
                            account.MailboxEnabled,
                            account.MailboxQuotaError,
                            authErrorType, 
                            account.MailboxSignature, 
                            account.MailboxAutoreply,
                            !string.IsNullOrEmpty(account.MailboxOAuthToken),
                            account.MailboxEmailInFolder,
                            account.MailboxIsServerMailbox,
                            account.ServerDomainTenant == Defines.SHARED_TENANT_ID);

                        if (group != null) accountInfo.Groups.Add(group);

                        accountInfoList.Add(accountInfo);
                    }
                    else if (group != null)
                    {
                        accountInfoList[accountIndex].Groups.Add(group);
                    }
                }
                else
                {
                    var alias = new MailAddressInfo(account.ServerAddressId,
                        string.Format("{0}@{1}", account.ServerAddressName, account.ServerDomainName),
                        account.ServerDomainId);

                    accountInfoList[accountIndex].Aliases.Add(alias);
                }
            }

            //CacheEngine.Set(User, accountInfoList);

            return accountInfoList;
        }

        public AccountInfo TryCreateAccount(AccountModel accountModel, out LoginResult loginResult)
        {
            if (accountModel == null)
                throw new NullReferenceException("accountModel");

            var mbox = new MailBoxData
            {
                Name = accountModel.Name,
                EMail = new MailAddress(accountModel.Email),
                Account = accountModel.Login,
                Password = accountModel.Password,
                Port = accountModel.Port,
                Server = accountModel.Server,
                SmtpAccount = accountModel.SmtpLogin,
                SmtpPassword = accountModel.SmtpPassword,
                SmtpPort = accountModel.SmtpPort,
                SmtpServer = accountModel.SmtpServer,
                Imap = accountModel.Imap,
                TenantId = Tenant,
                UserId = UserId,
                BeginDate = accountModel.Restrict
                    ? DateTime.Now.Subtract(new TimeSpan(MailBoxData.DefaultMailLimitedTimeDelta))
                    : new DateTime(MailBoxData.DefaultMailBeginTimestamp),
                Encryption = accountModel.IncomingEncryptionType,
                SmtpEncryption = accountModel.OutcomingEncryptionType,
                Authentication = accountModel.IncomingAuthenticationType,
                SmtpAuthentication = accountModel.SmtpAuth ? accountModel.OutcomingAuthenticationType : SaslMechanism.None,
                Enabled = true
            };

            using (var client = new MailClient(mbox, CancellationToken.None,
                    certificatePermit: Defines.SslCertificatesErrorPermit, log: Log))
            {
                loginResult = client.TestLogin();
            }

            if (!loginResult.IngoingSuccess || !loginResult.OutgoingSuccess)
                return null;

            if (!MailboxEngine.SaveMailBox(mbox))
                throw new Exception(string.Format("SaveMailBox {0} failed", mbox.EMail));

            //CacheEngine.Clear(User);

            var account = new AccountInfo(mbox.MailBoxId, mbox.EMailView, mbox.Name, mbox.Enabled, mbox.QuotaError,
                MailBoxData.AuthProblemType.NoProblems, new MailSignatureData(mbox.MailBoxId, Tenant, "", false),
                new MailAutoreplyData(mbox.MailBoxId, Tenant, false, false, false, DateTime.MinValue,
                    DateTime.MinValue, string.Empty, string.Empty), false, mbox.EMailInFolder, false, false);

            return account;
        }

        //public AccountInfo CreateAccountSimple(string email, string password, out List<LoginResult> loginResults)
        //{
        //    MailBoxData mbox = null;

        //    var domain = email.Substring(email.IndexOf('@') + 1);

        //    var engine = new EngineFactory(Tenant, User);

        //    var mailboxSettings = engine.MailBoxSettingEngine.GetMailBoxSettings(domain);

        //    if (mailboxSettings == null)
        //    {
        //        throw new Exception("Unknown mail provider settings.");
        //    }

        //    var testMailboxes = mailboxSettings.ToMailboxList(email, password, Tenant, User);

        //    loginResults = new List<LoginResult>();

        //    foreach (var mb in testMailboxes)
        //    {
        //        LoginResult loginResult;

        //        using (var client = new MailClient(mb, CancellationToken.None, Defines.TcpTimeout,
        //                Defines.SslCertificatesErrorPermit, log: Log))
        //        {
        //            loginResult = client.TestLogin();
        //        }

        //        loginResults.Add(loginResult);

        //        if (!loginResult.IngoingSuccess || !loginResult.OutgoingSuccess)
        //            continue;

        //        mbox = mb;
        //        break;
        //    }

        //    if (mbox == null)
        //        return null;

        //    if (!MailboxEngine.SaveMailBox(mbox))
        //        throw new Exception(string.Format("SaveMailBox {0} failed", email));

        //    CacheEngine.Clear(User);

        //    var account = new AccountInfo(mbox.MailBoxId, mbox.EMailView, mbox.Name, mbox.Enabled, mbox.QuotaError,
        //        MailBoxData.AuthProblemType.NoProblems, new MailSignatureData(mbox.MailBoxId, Tenant, "", false),
        //        new MailAutoreplyData(mbox.MailBoxId, Tenant, false, false, false, DateTime.MinValue,
        //            DateTime.MinValue, string.Empty, string.Empty), false, mbox.EMailInFolder, false, false);

        //    return account;
        //}

        //public AccountInfo CreateAccountOAuth(string code, byte type)
        //{
        //    var oAuthToken = OAuth20TokenHelper.GetAccessToken<GoogleLoginProvider>(code);

        //    if (oAuthToken == null)
        //        throw new Exception(@"Empty oauth token");

        //    var loginProfile = GoogleLoginProvider.Instance.GetLoginProfile(oAuthToken.AccessToken);
        //    var email = loginProfile.EMail;

        //    if (string.IsNullOrEmpty(email))
        //        throw new Exception(@"Empty email");

        //    var beginDate = DateTime.UtcNow.Subtract(new TimeSpan(MailBoxData.DefaultMailLimitedTimeDelta));

        //    var mboxImap = MailboxEngine.GetDefaultMailboxData(email, "", (AuthorizationServiceType)type,
        //        true, false);

        //    mboxImap.OAuthToken = oAuthToken.ToJson();
        //    mboxImap.BeginDate = beginDate; // Apply restrict for download

        //    if (!MailboxEngine.SaveMailBox(mboxImap, (AuthorizationServiceType)type))
        //        throw new Exception(string.Format("SaveMailBox {0} failed", email));

        //    CacheEngine.Clear(User);

        //    if (Defines.IsSignalRAvailable)
        //    {
        //        var engine = new EngineFactory(Tenant, User);
        //        engine.AccountEngine.SetAccountsActivity();
        //    }

        //    var account = new AccountInfo(mboxImap.MailBoxId, mboxImap.EMailView, mboxImap.Name, mboxImap.Enabled,
        //        mboxImap.QuotaError,
        //        MailBoxData.AuthProblemType.NoProblems, new MailSignatureData(mboxImap.MailBoxId, Tenant, "", false),
        //        new MailAutoreplyData(mboxImap.MailBoxId, Tenant, false, false, false, DateTime.MinValue,
        //            DateTime.MinValue, string.Empty, string.Empty), true, mboxImap.EMailInFolder, false, false);

        //    return account;
        //}

        //public AccountInfo UpdateAccount(MailBoxData newMailBoxData, out LoginResult loginResult)
        //{
        //    if (newMailBoxData == null)
        //        throw new NullReferenceException("mbox");

        //    Mailbox mbox;

        //    using (var daoFactory = new DaoFactory())
        //    {
        //        var daoMailbox = daoFactory.CreateMailboxDao();

        //        mbox =
        //            daoMailbox.GetMailBox(
        //                new СoncreteUserMailboxExp(
        //                    newMailBoxData.EMail,
        //                    Tenant, User));

        //        if (null == mbox)
        //            throw new ArgumentException("Mailbox with specified email doesn't exist.");

        //        if (mbox.IsTeamlabMailbox)
        //            throw new ArgumentException("Mailbox with specified email can't be updated");

        //        if (!string.IsNullOrEmpty(mbox.OAuthToken))
        //        {
        //            var needSave = false;

        //            if (!mbox.Name.Equals(newMailBoxData.Name))
        //            {
        //                mbox.Name = newMailBoxData.Name;
        //                needSave = true;
        //            }

        //            if (!mbox.BeginDate.Equals(newMailBoxData.BeginDate))
        //            {
        //                mbox.BeginDate = newMailBoxData.BeginDate;
        //                mbox.ImapIntervals = null;
        //                needSave = true;
        //            }

        //            if (needSave)
        //            {
        //                daoMailbox.SaveMailBox(mbox);

        //                CacheEngine.Clear(User);
        //            }

        //            var accountInfo = new AccountInfo(mbox.Id, mbox.Address, mbox.Name, mbox.Enabled, mbox.QuotaError,
        //                MailBoxData.AuthProblemType.NoProblems, new MailSignatureData(mbox.Id, Tenant, "", false),
        //                new MailAutoreplyData(mbox.Id, Tenant, false, false, false, DateTime.MinValue,
        //                    DateTime.MinValue, string.Empty, string.Empty), false, mbox.EmailInFolder, false, false);

        //            loginResult = new LoginResult
        //            {
        //                Imap = mbox.Imap,
        //                IngoingSuccess = true,
        //                OutgoingSuccess = true
        //            };

        //            return accountInfo;
        //        }
        //    }

        //    newMailBoxData.Password = string.IsNullOrEmpty(newMailBoxData.Password)
        //        ? mbox.Password
        //        : newMailBoxData.Password;

        //    newMailBoxData.SmtpPassword = string.IsNullOrEmpty(newMailBoxData.SmtpPassword)
        //        ? mbox.SmtpPassword
        //        : newMailBoxData.SmtpPassword;

        //    newMailBoxData.Imap = mbox.Imap;

        //    return CreateAccount(newMailBoxData, out loginResult);
        //}

        //public AccountInfo UpdateAccountOAuth(int mailboxId, string code, byte type)
        //{
        //    if (string.IsNullOrEmpty(code))
        //        throw new ArgumentException(@"Empty OAuth code", "code");

        //    var oAuthToken = OAuth20TokenHelper.GetAccessToken<GoogleLoginProvider>(code);

        //    if (oAuthToken == null)
        //        throw new Exception(@"Empty OAuth token");

        //    if (string.IsNullOrEmpty(oAuthToken.AccessToken))
        //        throw new Exception(@"Empty OAuth AccessToken");

        //    if (string.IsNullOrEmpty(oAuthToken.RefreshToken))
        //        throw new Exception(@"Empty OAuth RefreshToken");

        //    if (oAuthToken.IsExpired)
        //        throw new Exception(@"OAuth token is expired");

        //    var loginProfile = GoogleLoginProvider.Instance.GetLoginProfile(oAuthToken.AccessToken);
        //    var email = loginProfile.EMail;

        //    if (string.IsNullOrEmpty(email))
        //        throw new Exception(@"Empty email");

        //    Mailbox mbox;

        //    using (var daoFactory = new DaoFactory())
        //    {
        //        var daoMailbox = daoFactory.CreateMailboxDao();

        //        mbox = daoMailbox.GetMailBox(
        //            new СoncreteUserMailboxExp(
        //                mailboxId,
        //                Tenant, User));

        //        if (null == mbox)
        //            throw new ArgumentException("Mailbox with specified email doesn't exist.");

        //        if (mbox.IsTeamlabMailbox || string.IsNullOrEmpty(mbox.OAuthToken))
        //            throw new ArgumentException("Mailbox with specified email can't be updated");

        //        if (!mbox.Address.Equals(email, StringComparison.InvariantCultureIgnoreCase))
        //            throw new ArgumentException("Mailbox with specified email can't be updated");

        //        mbox.OAuthToken = oAuthToken.ToJson();

        //        var result = daoMailbox.SaveMailBox(mbox);

        //        mbox.Id = result;
        //    }

        //    CacheEngine.Clear(User);

        //    if (Defines.IsSignalRAvailable)
        //    {
        //        var engine = new EngineFactory(Tenant, User);
        //        engine.AccountEngine.SetAccountsActivity();
        //    }

        //    var accountInfo = new AccountInfo(mbox.Id, mbox.Address, mbox.Name, mbox.Enabled, mbox.QuotaError,
        //        MailBoxData.AuthProblemType.NoProblems, new MailSignatureData(mbox.Id, Tenant, "", false),
        //        new MailAutoreplyData(mbox.Id, Tenant, false, false, false, DateTime.MinValue,
        //            DateTime.MinValue, string.Empty, string.Empty), true, mbox.EmailInFolder, false, false);

        //    return accountInfo;
        //}

        //public int SetAccountEnable(MailAddress address, bool enabled, out LoginResult loginResult)
        //{
        //    if (address == null)
        //        throw new ArgumentNullException("address");

        //    var engine = new EngineFactory(Tenant);

        //    var tuple = engine.MailboxEngine.GetMailboxFullInfo(new СoncreteUserMailboxExp(address, Tenant, User));

        //    if (tuple == null)
        //        throw new NullReferenceException(string.Format("Account wasn't found by email: {0}", address.Address));

        //    if (enabled)
        //    {
        //        // Check account connection setting on activation
        //        using (var client = new MailClient(tuple.Item1, CancellationToken.None,
        //                certificatePermit: Defines.SslCertificatesErrorPermit, log: Log))
        //        {
        //            loginResult = client.TestLogin();
        //        }

        //        if (!loginResult.IngoingSuccess || !loginResult.OutgoingSuccess)
        //        {
        //            return -1;
        //        }
        //    }

        //    int mailboxId;

        //    using (var daoFactory = new DaoFactory())
        //    {
        //        var daoMailbox = daoFactory.CreateMailboxDao();

        //        loginResult = null;
        //        mailboxId =
        //            daoMailbox.Enable(new СoncreteUserMailboxExp(tuple.Item2.Id, tuple.Item2.Tenant, tuple.Item2.User),
        //                enabled)
        //                ? tuple.Item2.Id
        //                : -1;
        //    }

        //    if (mailboxId == -1)
        //        return mailboxId;

        //    CacheEngine.Clear(User);

        //    return mailboxId;
        //}

        //public bool SetAccountEmailInFolder(int mailboxId, string emailInFolder)
        //{
        //    if (mailboxId < 0)
        //        throw new ArgumentNullException("mailboxId");

        //    bool saved;

        //    using (var daoFactory = new DaoFactory())
        //    {
        //        var daoMailbox = daoFactory.CreateMailboxDao();

        //        var mailbox = daoMailbox.GetMailBox(
        //            new СoncreteUserMailboxExp(
        //                mailboxId,
        //                Tenant, User)
        //            );

        //        if (mailbox == null)
        //            return false;

        //        saved = daoMailbox.SetMailboxEmailIn(mailbox, emailInFolder);
        //    }

        //    if (!saved)
        //        return saved;

        //    CacheEngine.Clear(User);

        //    return saved;
        //}

        //public bool SetAccountsActivity(bool userOnline = true)
        //{
        //    using (var daoFactory = new DaoFactory())
        //    {
        //        var daoMailbox = daoFactory.CreateMailboxDao();

        //        return daoMailbox.SetMailboxesActivity(Tenant, User, userOnline);
        //    }
        //}

        //public List<string> SearchAccountEmails(string searchText)
        //{
        //    var accounts = GetAccountInfoList();
        //    var emails = new List<string>();

        //    foreach (var account in accounts)
        //    {
        //        var email = string.IsNullOrEmpty(account.Name)
        //                        ? account.Email
        //                        : MailUtil.CreateFullEmail(account.Name, account.Email);
        //        emails.Add(email);

        //        foreach (var alias in account.Aliases)
        //        {
        //            email = string.IsNullOrEmpty(account.Name)
        //                        ? account.Email
        //                        : MailUtil.CreateFullEmail(account.Name, alias.Email);
        //            emails.Add(email);
        //        }

        //        foreach (var group in account.Groups.Where(group => emails.IndexOf(group.Email) == -1))
        //        {
        //            emails.Add(group.Email);
        //        }
        //    }

        //    return emails.Where(e => e.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) > -1).ToList();
        //}
    }

    public static class AccountEngineExtension
    {
        public static IServiceCollection AddAccountEngineService(this IServiceCollection services)
        {
            services.TryAddScoped<AccountEngine>();

            return services;
        }
    }
}
