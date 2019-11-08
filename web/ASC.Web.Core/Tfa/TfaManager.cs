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
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using ASC.Common.Caching;
using ASC.Common.Utils;
using ASC.Core;
using ASC.Core.Common.Security;
using ASC.Core.Common.Settings;
using ASC.Core.Users;
using ASC.Web.Core;
using ASC.Web.Core.PublicResources;
using Google.Authenticator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ASC.Web.Studio.Core.TFA
{
    [Serializable]
    [DataContract]
    public class BackupCode
    {
        [DataMember(Name = "Code")]
        private string code;

        public Signature Signature { get; }

        public string Code
        {
            get { return Signature.Read<string>(code); }
            set { code = Signature.Create(value); }
        }

        [DataMember(Name = "IsUsed")]
        public bool IsUsed { get; set; }

        public BackupCode(Signature signature, string code)
        {
            Signature = signature;
            Code = code;
            IsUsed = false;
        }
    }

    public class TfaManager
    {
        private static readonly TwoFactorAuthenticator Tfa = new TwoFactorAuthenticator();
        private static readonly ICache Cache = AscCache.Memory;

        public SettingsManager SettingsManager { get; }
        public SecurityContext SecurityContext { get; }
        public CookiesManager CookiesManager { get; }
        public SetupInfo SetupInfo { get; }
        public Signature Signature { get; }

        public TfaManager(
            SettingsManager settingsManager,
            SecurityContext securityContext,
            CookiesManager cookiesManager,
            SetupInfo setupInfo,
            Signature signature)
        {
            SettingsManager = settingsManager;
            SecurityContext = securityContext;
            CookiesManager = cookiesManager;
            SetupInfo = setupInfo;
            Signature = signature;
        }

        public SetupCode GenerateSetupCode(UserInfo user, int size)
        {
            return Tfa.GenerateSetupCode(SetupInfo.TfaAppSender, user.Email, GenerateAccessToken(user), size, size, true);
        }

        public bool ValidateAuthCode(UserInfo user, int tenantId, string code, bool checkBackup = true)
        {
            if (!TfaAppAuthSettings.IsVisibleSettings
                || !SettingsManager.Load<TfaAppAuthSettings>().EnableSetting)
            {
                return false;
            }

            if (user == null || Equals(user, Constants.LostUser)) throw new Exception(Resource.ErrorUserNotFound);

            code = (code ?? "").Trim();

            if (string.IsNullOrEmpty(code)) throw new Exception(Resource.ActivateTfaAppEmptyCode);

            int.TryParse(Cache.Get<string>("tfa/" + user.ID), out var counter);
            if (++counter > SetupInfo.LoginThreshold)
            {
                throw new BruteForceCredentialException(Resource.TfaTooMuchError);
            }
            Cache.Insert("tfa/" + user.ID, counter.ToString(CultureInfo.InvariantCulture), DateTime.UtcNow.Add(TimeSpan.FromMinutes(1)));

            if (!Tfa.ValidateTwoFactorPIN(GenerateAccessToken(user), code))
            {
                if (checkBackup && TfaAppUserSettings.BackupCodesForUser(SettingsManager, user.ID).Any(x => x.Code == code && !x.IsUsed))
                {
                    TfaAppUserSettings.DisableCodeForUser(SettingsManager, user.ID, code);
                }
                else
                {
                    throw new ArgumentException(Resource.TfaAppAuthMessageError);
                }
            }

            Cache.Insert("tfa/" + user.ID, (--counter).ToString(CultureInfo.InvariantCulture), DateTime.UtcNow.Add(TimeSpan.FromMinutes(1)));

            if (!SecurityContext.IsAuthenticated)
            {
                var cookiesKey = SecurityContext.AuthenticateMe(user.ID);
                CookiesManager.SetCookies(CookiesType.AuthKey, cookiesKey);
            }

            if (!TfaAppUserSettings.EnableForUser(SettingsManager, user.ID))
            {
                GenerateBackupCodes(user);
                return true;
            }

            return false;
        }

        public IEnumerable<BackupCode> GenerateBackupCodes(UserInfo user)
        {
            var count = SetupInfo.TfaAppBackupCodeCount;
            var length = SetupInfo.TfaAppBackupCodeLength;

            const string alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890-_";

            var data = new byte[length];

            var list = new List<BackupCode>();

            using (var rngCrypto = new RNGCryptoServiceProvider())
            {
                for (var i = 0; i < count; i++)
                {
                    rngCrypto.GetBytes(data);

                    var result = new StringBuilder(length);
                    foreach (var b in data)
                    {
                        result.Append(alphabet[b % (alphabet.Length)]);
                    }

                    list.Add(new BackupCode(Signature, result.ToString()));
                }
            }
            var settings = SettingsManager.LoadForCurrentUser<TfaAppUserSettings>();
            settings.CodesSetting = list;
            SettingsManager.SaveForCurrentUser(settings);

            return list;
        }

        private string GenerateAccessToken(UserInfo user)
        {
            return Signature.Create(TfaAppUserSettings.GetSalt(SettingsManager, user.ID)).Substring(0, 10);
        }
    }

    public static class TfaManagerFactory
    {
        public static IServiceCollection AddTfaManagerService(this IServiceCollection services)
        {
            services.TryAddScoped<TfaManager>();

            return services
                .AddSettingsManagerService()
                .AddSetupInfo()
                .AddSignatureService()
                .AddCookiesManagerService()
                .AddSecurityContextService();
        }
    }
}