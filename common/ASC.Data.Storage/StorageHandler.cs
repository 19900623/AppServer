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
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using ASC.Common.Web;
using ASC.Core;
using ASC.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ASC.Data.Storage.DiscStorage
{
    public class StorageHandler
    {
        private readonly string _path;
        private readonly string _module;
        private readonly string _domain;
        private readonly bool _checkAuth;

        public StorageHandler(IServiceProvider serviceProvider, string path, string module, string domain, bool checkAuth = true)
        {
            ServiceProvider = serviceProvider;
            _path = path;
            _module = module;
            _domain = domain;
            _checkAuth = checkAuth;
        }

        public IServiceProvider ServiceProvider { get; }

        public async Task Invoke(HttpContext context)
        {
            using var scope = ServiceProvider.CreateScope();
            var SecurityContext = scope.ServiceProvider.GetService<SecurityContext>();

            if (_checkAuth && !SecurityContext.IsAuthenticated)
            {
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                return;
            }

            var storage = StorageFactory.GetStorage(CoreContext.TenantManager.GetCurrentTenant().TenantId.ToString(CultureInfo.InvariantCulture), _module);
            var path = Path.Combine(_path, GetRouteValue("pathInfo").Replace('/', Path.DirectorySeparatorChar));
            var header = context.Request.Query[Constants.QUERY_HEADER].FirstOrDefault() ?? "";

            var auth = context.Request.Query[Constants.QUERY_AUTH].FirstOrDefault() ?? "";
            var storageExpire = storage.GetExpire(_domain);

            if (storageExpire != TimeSpan.Zero && storageExpire != TimeSpan.MinValue && storageExpire != TimeSpan.MaxValue || !string.IsNullOrEmpty(auth))
            {
                var expire = context.Request.Query[Constants.QUERY_EXPIRE];
                if (string.IsNullOrEmpty(expire)) expire = storageExpire.TotalMinutes.ToString(CultureInfo.InvariantCulture);

                var validateResult = EmailValidationKeyProvider.ValidateEmailKey(path + "." + header + "." + expire, auth ?? "", TimeSpan.FromMinutes(Convert.ToDouble(expire)));
                if (validateResult != EmailValidationKeyProvider.ValidationResult.Ok)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    return;
                }
            }

            if (!storage.IsFile(_domain, path))
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            var headers = header.Length > 0 ? header.Split('&').Select(HttpUtility.UrlDecode) : new string[] { };

            const int bigSize = 5 * 1024 * 1024;
            if (storage.IsSupportInternalUri && bigSize < storage.GetFileSize(_domain, path))
            {
                var uri = storage.GetInternalUri(_domain, path, TimeSpan.FromMinutes(15), headers);

                //TODO
                //context.Response.Cache.SetAllowResponseInBrowserHistory(false);
                //context.Response.Cache.SetCacheability(HttpCacheability.NoCache);

                context.Response.Redirect(uri.ToString());
                return;
            }

            string encoding = null;
            if (storage is DiscDataStore && storage.IsFile(_domain, path + ".gz"))
            {
                path += ".gz";
                encoding = "gzip";
            }
            using (var stream = storage.GetReadStream(_domain, path))
            {
                await stream.StreamCopyToAsync(context.Response.Body);
            }

            foreach (var h in headers)
            {
                if (h.StartsWith("Content-Disposition")) context.Response.Headers["Content-Disposition"] = h.Substring("Content-Disposition".Length + 1);
                else if (h.StartsWith("Cache-Control")) context.Response.Headers["Cache-Control"] = h.Substring("Cache-Control".Length + 1);
                else if (h.StartsWith("Content-Encoding")) context.Response.Headers["Content-Encoding"] = h.Substring("Content-Encoding".Length + 1);
                else if (h.StartsWith("Content-Language")) context.Response.Headers["Content-Language"] = h.Substring("Content-Language".Length + 1);
                else if (h.StartsWith("Content-Type")) context.Response.Headers["Content-Type"] = h.Substring("Content-Type".Length + 1);
                else if (h.StartsWith("Expires")) context.Response.Headers["Expires"] = h.Substring("Expires".Length + 1);
            }

            context.Response.ContentType = MimeMapping.GetMimeMapping(path);
            if (encoding != null)
                context.Response.Headers["Content-Encoding"] = encoding;

            string GetRouteValue(string name)
            {
                return (context.GetRouteValue(name) ?? "").ToString();
            }
        }
    }

    public static class StorageHandlerExtensions
    {
        public static IEndpointRouteBuilder RegisterStorageHandler(this IEndpointRouteBuilder builder, string module, string domain, bool publicRoute = false)
        {
            var virtPath = PathUtils.ResolveVirtualPath(module, domain);
            virtPath = virtPath.TrimStart('/');
            
            var handler = new StorageHandler(builder.ServiceProvider, string.Empty, module, domain, !publicRoute);
            var url = virtPath + "{*pathInfo}";

            if (!builder.DataSources.Any(r => r.Endpoints.Any(e => e.DisplayName == url)))
            {
                builder.Map(url, handler.Invoke);

                var newUrl = url.Replace("{0}", "{t1}/{t2}/{t3}");

                if (newUrl != url)
                {
                    builder.Map(url, handler.Invoke);
                }
            }

            return builder;
        }
    }
}