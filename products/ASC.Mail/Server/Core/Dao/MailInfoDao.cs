/*
 *
 * (c) Copyright Ascensio System Limited 2010-2020
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
using System.Reflection;
using ASC.Common;
using ASC.Core;
using ASC.Core.Common.EF;
using ASC.Mail.Core.Dao.Entities;
using ASC.Mail.Core.Dao.Expressions.Message;
using ASC.Mail.Core.Dao.Interfaces;
using ASC.Mail.Core.Entities;
using ASC.Mail.Enums;

namespace ASC.Mail.Core.Dao
{
    public class MailInfoDao : BaseDao, IMailInfoDao
    {
        public MailInfoDao(
             TenantManager tenantManager,
             SecurityContext securityContext,
             DbContextManager<MailDbContext> dbContext)
            : base(tenantManager, securityContext, dbContext)
        { 
        }

        public List<MailInfo> GetMailInfoList(IMessagesExp exp, bool skipSelectTags = false)
        {
            var query = MailDb.MailMail
                .Where(exp.GetExpression());

            if (exp.TagIds != null && exp.TagIds.Any())
            {
                query.Join(MailDb.MailTagMail, m => m.Id, tm => tm.IdMail,
                    (m, tm) => new
                    {
                        Mail = m,
                        Xtags = tm
                    })
                    .Where(g => exp.TagIds.Contains(g.Xtags.IdTag))
                    .GroupBy(g => g.Mail.Id)
                    .Where(g => g.Count() == exp.TagIds.Count);
            }

            if (exp.UserFolderId.HasValue)
            {
                query.Join(MailDb.MailUserFolderXMail, m => m.Id, x => (int)x.IdMail,
                    (m, x) => new
                    {
                        Mail = m,
                        XuserFolder = x
                    })
                    .Where(g => g.XuserFolder.IdFolder == exp.UserFolderId.Value);
            }

            if(exp.StartIndex.HasValue)
            {
                query.Skip(exp.StartIndex.Value);
            }

            if (exp.Limit.HasValue)
            {
                query.Take(exp.Limit.Value);
            }

            if (!string.IsNullOrEmpty(exp.OrderBy))
            {
                var sortField = "DateSent";

                if (exp.OrderBy == Defines.ORDER_BY_SUBJECT)
                {
                    sortField = "Subject";
                }
                else if (exp.OrderBy == Defines.ORDER_BY_SENDER)
                {
                    sortField = "FromText";
                }
                else if (exp.OrderBy == Defines.ORDER_BY_DATE_CHAIN)
                {
                    sortField = "ChainDate";
                }

                query.OrderBy(sortField, exp.OrderAsc.GetValueOrDefault());
            }

            var list = query
                .Select(m => new { 
                    Mail = m,
                    LabelsString = skipSelectTags ? "" : string.Join(",", MailDb.MailTagMail.Where(t => t.IdMail == m.Id).Select(t => t.IdTag))
                })
                .Select(x => ToMailInfo(x.Mail, x.LabelsString))
                .ToList();

            return list;
        }

        public long GetMailInfoTotal(IMessagesExp exp)
        {
            var query = MailDb.MailMail
                .Where(exp.GetExpression());

            if (exp.TagIds != null && exp.TagIds.Any())
            {
                query.Join(MailDb.MailTagMail, m => m.Id, tm => tm.IdMail,
                    (m, tm) => new
                    {
                        Mail = m,
                        Xtags = tm
                    })
                    .Where(g => exp.TagIds.Contains(g.Xtags.IdTag))
                    .GroupBy(g => g.Mail.Id)
                    .Where(g => g.Count() == exp.TagIds.Count);
            }

            if (exp.UserFolderId.HasValue)
            {
                query.Join(MailDb.MailUserFolderXMail, m => m.Id, x => (int)x.IdMail,
                    (m, x) => new
                    {
                        Mail = m,
                        XuserFolder = x
                    })
                    .Where(g => g.XuserFolder.IdFolder == exp.UserFolderId.Value);
            }

            var total = query.Count();

            return total;
        }

        public Dictionary<int, int> GetMailCount(IMessagesExp exp)
        {
            var dictionary = MailDb.MailMail
                .Where(exp.GetExpression())
                .GroupBy(m => m.Folder)
                .Select(g => new
                {
                    FolderId = g.Key,
                    Count = g.Count()
                })
                .ToDictionary(o => o.FolderId, o => o.Count);

            return dictionary;
        }

        public Dictionary<uint, int> GetMailUserFolderCount(List<int> userFolderIds, bool? unread = null)
        {
            var dictionary = (from t in MailDb.MailUserFolderXMail
                              join m in MailDb.MailMail on (int)t.IdMail equals m.Id into UFxMail
                              from ufxm in UFxMail
                              where t.Tenant == Tenant && t.IdUser == UserId
                                && userFolderIds.Contains((int)t.IdFolder)
                                && (unread.HasValue && ufxm.Unread == unread.Value)
                              group t by t.IdFolder into UFCounters
                              select new
                              {
                                  FolderId = UFCounters.Key,
                                  Count = UFCounters.Count()
                              })
                             .ToList()
                             .ToDictionary(o => o.FolderId, o => o.Count);

            return dictionary;
        }

        public Dictionary<uint, int> GetMailUserFolderCount(bool? unread = null)
        {
            var dictionary = (from t in MailDb.MailUserFolderXMail
                              join m in MailDb.MailMail on (int)t.IdMail equals m.Id into UFxMail
                              from ufxm in UFxMail
                              where t.Tenant == Tenant && t.IdUser == UserId
                                && (unread.HasValue && ufxm.Unread == unread.Value)
                              group t by t.IdFolder into UFCounters
                              select new
                              {
                                  FolderId = UFCounters.Key,
                                  Count = UFCounters.Count()
                              })
                             .ToList()
                             .ToDictionary(o => o.FolderId, o => o.Count);

            return dictionary;
        }

        public Tuple<int, int> GetRangeMails(IMessagesExp exp)
        {
            var max = MailDb.MailMail
                .Where(exp.GetExpression())
                .Max(m => m.Id);

            var min = MailDb.MailMail
                .Where(exp.GetExpression())
                .Min(m => m.Id);

            return new Tuple<int, int>(min, max);
        }

        public T GetFieldMaxValue<T>(IMessagesExp exp, string field)
        {
            Type type = typeof(T);
            PropertyInfo pi = type.GetProperty(field);

            if (pi == null)
                throw new ArgumentException("Field not found");

            var max = MailDb.MailMail
                .Where(exp.GetExpression())
                .Max(m => pi.GetValue(m));

            return (T)max;
        }

        public int SetFieldValue<T>(IMessagesExp exp, string field, T value)
        {
            Type type = typeof(T);
            PropertyInfo pi = type.GetProperty(field);

            if (pi == null)
                throw new ArgumentException("Field not found");

            var mails = MailDb.MailMail
                .Where(exp.GetExpression())
                .ToList();

            foreach (var mail in mails)
            {
                pi.SetValue(mail, Convert.ChangeType(value, pi.PropertyType), null);
            }

            var result = MailDb.SaveChanges();

            return result;
        }

        public int SetFieldsEqual(IMessagesExp exp, string fieldFrom, string fieldTo)
        {
            Type type = typeof(MailMail);
            PropertyInfo piFrom = type.GetProperty(fieldFrom);
            PropertyInfo piTo = type.GetProperty(fieldTo);

            if (piFrom == null)
                throw new ArgumentException("FieldFrom not found");

            if (piTo == null)
                throw new ArgumentException("FieldTo not found");

            var mails = MailDb.MailMail
                .Where(exp.GetExpression())
                .ToList();

            foreach (var mail in mails)
            {
                var value = piFrom.GetValue(mail);

                piTo.SetValue(mail, Convert.ChangeType(value, piFrom.PropertyType), null);
            }

            var result = MailDb.SaveChanges();

            return result;
        }

        protected MailInfo ToMailInfo(MailMail r, string labelsString)
        {
            var mailInfo = new MailInfo
            {
                Id = r.Id,
                From = r.FromText,
                To = r.ToText,
                Cc = r.Cc,
                ReplyTo = r.ReplyTo,
                Subject = r.Subject,
                Importance = r.Importance,
                DateSent = r.DateSent,
                Size = r.Size,
                HasAttachments = r.AttachmentsCount > 0,
                IsNew = r.Unread,
                IsAnswered = r.IsAnswered,
                IsForwarded = r.IsForwarded,
                LabelsString = labelsString,
                FolderRestore = (FolderType) r.Folder,
                Folder = (FolderType) r.FolderRestore,
                ChainId = r.ChainId,
                ChainDate = r.ChainDate,
                MailboxId = r.IdMailbox,
                CalendarUid = string.IsNullOrEmpty(r.CalendarUid) ? null : r.CalendarUid,
                Stream = r.Stream,
                Uidl = r.Uidl,
                IsRemoved = r.IsRemoved,
                Intoduction = r.Introduction
            };

            return mailInfo;
        }
    }

    public static class MailInfoDaoExtension
    {
        public static DIHelper AddMailInfoDaoService(this DIHelper services)
        {
            services.TryAddScoped<MailInfoDao>();

            return services;
        }
    }
}