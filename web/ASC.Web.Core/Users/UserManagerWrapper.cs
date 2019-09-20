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
using System.Globalization;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using ASC.Core;
using ASC.Core.Tenants;
using ASC.Core.Users;
using ASC.IPSecurity;
using ASC.MessagingSystem;
using ASC.Web.Core.PublicResources;
using ASC.Web.Core.Utility;
using ASC.Web.Studio.Core.Notify;
using Microsoft.AspNetCore.Http;

namespace ASC.Web.Core.Users
{
    /// <summary>
    /// Web studio user manager helper
    /// </summary>
    public sealed class UserManagerWrapper
    {
        public StudioNotifyService StudioNotifyService { get; }
        public UserManager UserManager { get; }
        public SecurityContext SecurityContext { get; }
        public AuthContext AuthContext { get; }
        public TenantManager TenantManager { get; }
        public PasswordSettings PasswordSettings { get; }
        public MessageService MessageService { get; }
        public CustomNamingPeople CustomNamingPeople { get; }
        public TenantUtil TenantUtil { get; }
        public CoreBaseSettings CoreBaseSettings { get; }
        public IPSecurity.IPSecurity IPSecurity { get; }


        public UserManagerWrapper(
            StudioNotifyService studioNotifyService, 
            UserManager userManager, 
            SecurityContext securityContext, 
            AuthContext authContext,
            PasswordSettings passwordSettings,
            MessageService messageService,
            CustomNamingPeople customNamingPeople,
            TenantUtil tenantUtil,
            CoreBaseSettings coreBaseSettings,
            IPSecurity.IPSecurity iPSecurity
            )
        {
            StudioNotifyService = studioNotifyService;
            UserManager = userManager;
            SecurityContext = securityContext;
            AuthContext = authContext;
            PasswordSettings = passwordSettings;
            MessageService = messageService;
            CustomNamingPeople = customNamingPeople;
            TenantUtil = tenantUtil;
            CoreBaseSettings = coreBaseSettings;
            IPSecurity = iPSecurity;
        }

        private bool TestUniqueUserName(string uniqueName)
        {
            if (string.IsNullOrEmpty(uniqueName))
                return false;
            return Equals(UserManager.GetUserByUserName(uniqueName), Constants.LostUser);
        }

        private string MakeUniqueName(UserInfo userInfo)
        {
            if (string.IsNullOrEmpty(userInfo.Email))
                throw new ArgumentException(Resource.ErrorEmailEmpty, "userInfo");

            var uniqueName = new MailAddress(userInfo.Email).User;
            var startUniqueName = uniqueName;
            var i = 0;
            while (!TestUniqueUserName(uniqueName))
            {
                uniqueName = string.Format("{0}{1}", startUniqueName, (++i).ToString(CultureInfo.InvariantCulture));
            }
            return uniqueName;
        }

        public bool CheckUniqueEmail(Guid userId, string email)
        {
            var foundUser = UserManager.GetUserByEmail(email);
            return Equals(foundUser, Constants.LostUser) || foundUser.ID == userId;
        }

        public UserInfo AddUser(UserInfo userInfo, string password, bool afterInvite = false, bool notify = true, bool isVisitor = false, bool fromInviteLink = false, bool makeUniqueName = true)
        {
            if (userInfo == null) throw new ArgumentNullException("userInfo");

            if (!UserFormatter.IsValidUserName(userInfo.FirstName, userInfo.LastName))
                throw new Exception(Resource.ErrorIncorrectUserName);

            CheckPasswordPolicy(password);

            if (!CheckUniqueEmail(userInfo.ID, userInfo.Email))
                throw new Exception(CustomNamingPeople.Substitute<Resource>("ErrorEmailAlreadyExists"));
            if (makeUniqueName)
            {
                userInfo.UserName = MakeUniqueName(userInfo);
            }
            if (!userInfo.WorkFromDate.HasValue)
            {
                userInfo.WorkFromDate = TenantUtil.DateTimeNow();
            }

            if (!CoreBaseSettings.Personal && !fromInviteLink)
            {
                userInfo.ActivationStatus = !afterInvite ? EmployeeActivationStatus.Pending : EmployeeActivationStatus.Activated;
            }

            var newUserInfo = UserManager.SaveUserInfo(userInfo, isVisitor);
            SecurityContext.SetUserPassword(newUserInfo.ID, password);

            if (CoreBaseSettings.Personal)
            {
                StudioNotifyService.SendUserWelcomePersonal(newUserInfo);
                return newUserInfo;
            }

            if ((newUserInfo.Status & EmployeeStatus.Active) == EmployeeStatus.Active && notify)
            {
                //NOTE: Notify user only if it's active
                if (afterInvite)
                {
                    if (isVisitor)
                    {
                        StudioNotifyService.GuestInfoAddedAfterInvite(newUserInfo);
                    }
                    else
                    {
                        StudioNotifyService.UserInfoAddedAfterInvite(newUserInfo);
                    }

                    if (fromInviteLink)
                    {
                        StudioNotifyService.SendEmailActivationInstructions(newUserInfo, newUserInfo.Email);
                    }
                }
                else
                {
                    //Send user invite
                    if (isVisitor)
                    {
                        StudioNotifyService.GuestInfoActivation(newUserInfo);
                    }
                    else
                    {
                        StudioNotifyService.UserInfoActivation(newUserInfo);
                    }

                }
            }

            if (isVisitor)
            {
                UserManager.AddUserIntoGroup(newUserInfo.ID, Constants.GroupVisitor.ID);
            }

            return newUserInfo;
        }

        #region Password

        public void CheckPasswordPolicy(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                throw new Exception(Resource.ErrorPasswordEmpty);

            var passwordSettingsObj = PasswordSettings.Load();

            if (!PasswordSettings.CheckPasswordRegex(CoreBaseSettings, passwordSettingsObj, password))
                throw new Exception(GenerateErrorMessage(passwordSettingsObj));
        }

        public UserInfo SendUserPassword(string email)
        {
            email = (email ?? "").Trim();
            if (!email.TestEmailRegex()) throw new ArgumentNullException("email", Resource.ErrorNotCorrectEmail);

            if (!IPSecurity.Verify())
            {
                throw new Exception(Resource.ErrorAccessRestricted);
            }

            var userInfo = UserManager.GetUserByEmail(email);
            if (!UserManager.UserExists(userInfo) || string.IsNullOrEmpty(userInfo.Email))
            {
                throw new Exception(string.Format(Resource.ErrorUserNotFoundByEmail, email));
            }
            if (userInfo.Status == EmployeeStatus.Terminated)
            {
                throw new Exception(Resource.ErrorDisabledProfile);
            }
            if (userInfo.IsLDAP())
            {
                throw new Exception(Resource.CouldNotRecoverPasswordForLdapUser);
            }
            if (userInfo.IsSSO())
            {
                throw new Exception(Resource.CouldNotRecoverPasswordForSsoUser);
            }

            StudioNotifyService.UserPasswordChange(userInfo);

            var displayUserName = userInfo.DisplayUserName(false, UserManager);
            MessageService.Send(MessageAction.UserSentPasswordChangeInstructions, displayUserName);

            return userInfo;
        }

        private const string Noise = "1234567890mnbasdflkjqwerpoiqweyuvcxnzhdkqpsdk_-()=";

        public string GeneratePassword()
        {
            var ps = PasswordSettings.Load();

            var maxLength = PasswordSettings.MaxLength
                            - (ps.Digits ? 1 : 0)
                            - (ps.UpperCase ? 1 : 0)
                            - (ps.SpecSymbols ? 1 : 0);
            var minLength = Math.Min(ps.MinLength, maxLength);

            return string.Format("{0}{1}{2}{3}",
                                 GeneratePassword(minLength, minLength, Noise[0..^4]),
                                 ps.Digits ? GeneratePassword(1, 1, Noise.Substring(0, 10)) : string.Empty,
                                 ps.UpperCase ? GeneratePassword(1, 1, Noise.Substring(10, 20).ToUpper()) : string.Empty,
                                 ps.SpecSymbols ? GeneratePassword(1, 1, Noise.Substring(Noise.Length - 4, 4).ToUpper()) : string.Empty);
        }

        private static readonly Random Rnd = new Random();

        internal static string GeneratePassword(int minLength, int maxLength, string noise)
        {
            var length = Rnd.Next(minLength, maxLength + 1);

            var pwd = string.Empty;
            while (length-- > 0)
            {
                pwd += noise.Substring(Rnd.Next(noise.Length - 1), 1);
            }
            return pwd;
        }

        internal static string GenerateErrorMessage(PasswordSettings passwordSettings)
        {
            var error = new StringBuilder();

            error.AppendFormat("{0} ", Resource.ErrorPasswordMessage);
            error.AppendFormat(Resource.ErrorPasswordLength, passwordSettings.MinLength, PasswordSettings.MaxLength);
            if (passwordSettings.UpperCase)
                error.AppendFormat(", {0}", Resource.ErrorPasswordNoUpperCase);
            if (passwordSettings.Digits)
                error.AppendFormat(", {0}", Resource.ErrorPasswordNoDigits);
            if (passwordSettings.SpecSymbols)
                error.AppendFormat(", {0}", Resource.ErrorPasswordNoSpecialSymbols);

            return error.ToString();
        }

        public static string GetPasswordHelpMessage(PasswordSettings PasswordSettings)
        {
            var info = new StringBuilder();
            var passwordSettings = PasswordSettings.Load();
            info.AppendFormat("{0} ", Resource.ErrorPasswordMessageStart);
            info.AppendFormat(Resource.ErrorPasswordLength, passwordSettings.MinLength, PasswordSettings.MaxLength);
            if (passwordSettings.UpperCase)
                info.AppendFormat(", {0}", Resource.ErrorPasswordNoUpperCase);
            if (passwordSettings.Digits)
                info.AppendFormat(", {0}", Resource.ErrorPasswordNoDigits);
            if (passwordSettings.SpecSymbols)
                info.AppendFormat(", {0}", Resource.ErrorPasswordNoSpecialSymbols);

            return info.ToString();
        }

        #endregion

        public static bool ValidateEmail(string email)
        {
            const string pattern = @"^(([^<>()[\]\\.,;:\s@\""]+"
                                   + @"(\.[^<>()[\]\\.,;:\s@\""]+)*)|(\"".+\""))@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}"
                                   + @"\.[0-9]{1,3}\])|(([a-zA-Z\-0-9]+\.)+[a-zA-Z]{2,}))$";
            const RegexOptions options = RegexOptions.IgnoreCase | RegexOptions.Compiled;
            return new Regex(pattern, options).IsMatch(email);
        }
    }
}