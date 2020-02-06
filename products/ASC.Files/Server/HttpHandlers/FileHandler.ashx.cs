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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

using ASC.Common.Logging;
using ASC.Common.Web;
using ASC.Core;
using ASC.Files.Core;
using ASC.Files.Core.Data;
using ASC.Files.Core.Security;
using ASC.MessagingSystem;
using ASC.Security.Cryptography;
using ASC.Web.Core;
using ASC.Web.Core.Files;
using ASC.Web.Files.Classes;
using ASC.Web.Files.Core;
using ASC.Web.Files.Helpers;
using ASC.Web.Files.Resources;
using ASC.Web.Files.Services.DocumentService;
using ASC.Web.Files.Services.FFmpegService;
using ASC.Web.Files.Utils;
using ASC.Web.Studio.Core;
using ASC.Web.Studio.UserControls.Statistics;
using ASC.Web.Studio.Utility;

using JWT;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

using Newtonsoft.Json.Linq;

using File = ASC.Files.Core.File;
using FileShare = ASC.Files.Core.Security.FileShare;
using MimeMapping = ASC.Common.Web.MimeMapping;
using SecurityContext = ASC.Core.SecurityContext;

namespace ASC.Web.Files
{
    public class FileHandler //: AbstractHttpAsyncHandler
    {
        public string FileHandlerPath
        {
            get { return FilesLinkUtility.FileHandlerPath; }
        }

        public RequestDelegate Next { get; }
        public FilesLinkUtility FilesLinkUtility { get; }
        public TenantExtra TenantExtra { get; }
        public AuthContext AuthContext { get; }
        public SecurityContext SecurityContext { get; }
        public GlobalStore GlobalStore { get; }
        public IDaoFactory DaoFactory { get; }
        public FileSecurity FileSecurity { get; }
        public FileMarker FileMarker { get; }
        public SetupInfo SetupInfo { get; }
        public FileUtility FileUtility { get; }
        public Global Global { get; }
        public EmailValidationKeyProvider EmailValidationKeyProvider { get; }
        public CoreBaseSettings CoreBaseSettings { get; }
        public GlobalFolderHelper GlobalFolderHelper { get; }
        public PathProvider PathProvider { get; }
        public DocumentServiceTrackerHelper DocumentServiceTrackerHelper { get; }
        public FilesMessageService FilesMessageService { get; }
        public FileShareLink FileShareLink { get; }
        public FileConverter FileConverter { get; }
        public UserManager UserManager { get; }
        public ILog Logger { get; }
        public CookiesManager CookiesManager { get; }
        public TenantStatisticsProvider TenantStatisticsProvider { get; }

        public FileHandler(
            RequestDelegate next,
            FilesLinkUtility filesLinkUtility,
            TenantExtra tenantExtra,
            CookiesManager cookiesManager,
            AuthContext authContext,
            SecurityContext securityContext,
            GlobalStore globalStore,
            IOptionsMonitor<ILog> optionsMonitor,
            IDaoFactory daoFactory,
            FileSecurity fileSecurity,
            FileMarker fileMarker,
            SetupInfo setupInfo,
            FileUtility fileUtility,
            Global global,
            EmailValidationKeyProvider emailValidationKeyProvider,
            CoreBaseSettings coreBaseSettings,
            GlobalFolderHelper globalFolderHelper,
            PathProvider pathProvider,
            UserManager userManager,
            DocumentServiceTrackerHelper documentServiceTrackerHelper,
            FilesMessageService filesMessageService,
            FileShareLink fileShareLink,
            FileConverter fileConverter)
        {
            Next = next;
            FilesLinkUtility = filesLinkUtility;
            TenantExtra = tenantExtra;
            AuthContext = authContext;
            SecurityContext = securityContext;
            GlobalStore = globalStore;
            DaoFactory = daoFactory;
            FileSecurity = fileSecurity;
            FileMarker = fileMarker;
            SetupInfo = setupInfo;
            FileUtility = fileUtility;
            Global = global;
            EmailValidationKeyProvider = emailValidationKeyProvider;
            CoreBaseSettings = coreBaseSettings;
            GlobalFolderHelper = globalFolderHelper;
            PathProvider = pathProvider;
            DocumentServiceTrackerHelper = documentServiceTrackerHelper;
            FilesMessageService = filesMessageService;
            FileShareLink = fileShareLink;
            FileConverter = fileConverter;
            UserManager = userManager;
            Logger = optionsMonitor.CurrentValue;
            CookiesManager = cookiesManager;
        }

        public async Task Invoke(HttpContext context)
        {
            if (TenantExtra.IsNotPaid())
            {
                context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
                //context.Response.StatusDescription = "Payment Required.";
                return;
            }

            try
            {
                switch ((context.Request.Query[FilesLinkUtility.Action].FirstOrDefault() ?? "").ToLower())
                {
                    case "view":
                    case "download":
                        DownloadFile(context);
                        break;
                    case "bulk":
                        BulkDownloadFile(context);
                        break;
                    case "stream":
                        StreamFile(context);
                        break;
                    case "empty":
                        EmptyFile(context);
                        break;
                    case "tmp":
                        TempFile(context);
                        break;
                    case "create":
                        CreateFile(context);
                        break;
                    case "redirect":
                        Redirect(context);
                        break;
                    case "diff":
                        DifferenceFile(context);
                        break;
                    case "track":
                        TrackFile(context);
                        break;
                    default:
                        throw new HttpException((int)HttpStatusCode.BadRequest, FilesCommonResource.ErrorMassage_BadRequest);
                }

            }
            catch (InvalidOperationException e)
            {
                throw new HttpException((int)HttpStatusCode.InternalServerError, FilesCommonResource.ErrorMassage_BadRequest, e);
            }

            await Next.Invoke(context);
        }

        private void BulkDownloadFile(HttpContext context)
        {
            if (!SecurityContext.AuthenticateMe(CookiesManager.GetCookies(CookiesType.AuthKey)))
            {
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                return;
            }

            var store = GlobalStore.GetStore();
            var path = string.Format(@"{0}\{1}.zip", AuthContext.CurrentAccount.ID, FileConstant.DownloadTitle);
            if (!store.IsFile(FileConstant.StorageDomainTmp, path))
            {
                Logger.ErrorFormat("BulkDownload file error. File is not exist on storage. UserId: {0}.", AuthContext.CurrentAccount.ID);
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            if (store.IsSupportedPreSignedUri)
            {
                var url = store.GetPreSignedUri(FileConstant.StorageDomainTmp, path, TimeSpan.FromHours(1), null).ToString();
                context.Response.Redirect(url);
                return;
            }

            context.Response.Clear();

            try
            {
                var flushed = false;
                using (var readStream = store.GetReadStream(FileConstant.StorageDomainTmp, path))
                {
                    long offset = 0;
                    var length = readStream.Length;
                    if (readStream.CanSeek)
                    {
                        length = ProcessRangeHeader(context, readStream.Length, ref offset);
                        readStream.Seek(offset, SeekOrigin.Begin);
                    }

                    SendStreamByChunks(context, length, FileConstant.DownloadTitle + ".zip", readStream, ref flushed);
                }

                context.Response.Body.Flush();
                //context.Response.SuppressContent = true;
                //context.ApplicationInstance.CompleteRequest();
            }
            catch (Exception e)
            {
                Logger.ErrorFormat("BulkDownloadFile failed for user {0} with error: ", SecurityContext.CurrentAccount.ID, e.Message);
                throw new HttpException((int)HttpStatusCode.BadRequest, e.Message);
            }
        }

        private void DownloadFile(HttpContext context)
        {
            var flushed = false;
            try
            {
                var id = context.Request.Query[FilesLinkUtility.FileId];
                var doc = context.Request.Query[FilesLinkUtility.DocShareKey].FirstOrDefault() ?? "";

                var fileDao = DaoFactory.FileDao;
                var readLink = FileShareLink.Check(doc, true, fileDao, out var file);
                if (!readLink && file == null)
                {
                    fileDao.InvalidateCache(id);

                    file = int.TryParse(context.Request.Query[FilesLinkUtility.Version], out var version) && version > 0
                               ? fileDao.GetFile(id, version)
                               : fileDao.GetFile(id);
                }

                if (file == null)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;

                    return;
                }

                if (!readLink && !FileSecurity.CanRead(file))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    return;
                }

                if (!string.IsNullOrEmpty(file.Error)) throw new Exception(file.Error);

                if (!fileDao.IsExistOnStorage(file))
                {
                    Logger.ErrorFormat("Download file error. File is not exist on storage. File id: {0}.", file.ID);
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;

                    return;
                }

                FileMarker.RemoveMarkAsNew(file);

                context.Response.Clear();
                context.Response.Headers.Clear();
                //TODO
                //context.Response.Headers.Charset = "utf-8";

                FilesMessageService.Send(file, MessageAction.FileDownloaded, file.Title);

                if (string.Equals(context.Request.Headers["If-None-Match"], GetEtag(file)))
                {
                    //Its cached. Reply 304
                    context.Response.StatusCode = (int)HttpStatusCode.NotModified;
                    //context.Response.Cache.SetETag(GetEtag(file));
                }
                else
                {
                    //context.Response.CacheControl = "public";
                    //context.Response.Cache.SetETag(GetEtag(file));
                    //context.Response.Cache.SetCacheability(HttpCacheability.Public);

                    Stream fileStream = null;
                    try
                    {
                        var title = file.Title;

                        if (file.ContentLength <= SetupInfo.AvailableFileSize)
                        {
                            var ext = FileUtility.GetFileExtension(file.Title);

                            var outType = (context.Request.Query[FilesLinkUtility.OutType].FirstOrDefault() ?? "").Trim();
                            if (!string.IsNullOrEmpty(outType)
                                && FileUtility.ExtsConvertible.Keys.Contains(ext)
                                && FileUtility.ExtsConvertible[ext].Contains(outType))
                            {
                                ext = outType;
                            }

                            long offset = 0;
                            long length;
                            if (!file.ProviderEntry
                                && string.Equals(context.Request.Query["convpreview"], "true", StringComparison.InvariantCultureIgnoreCase)
                                && FFmpegService.IsConvertable(ext))
                            {
                                const string mp4Name = "content.mp4";
                                var mp4Path = FileDao.GetUniqFilePath(file, mp4Name);
                                var store = GlobalStore.GetStore();
                                if (!store.IsFile(mp4Path))
                                {
                                    fileStream = fileDao.GetFileStream(file);

                                    Logger.InfoFormat("Converting {0} (fileId: {1}) to mp4", file.Title, file.ID);
                                    var stream = FFmpegService.Convert(fileStream, ext);
                                    store.Save(string.Empty, mp4Path, stream, mp4Name);
                                }

                                var fullLength = store.GetFileSize(string.Empty, mp4Path);

                                length = ProcessRangeHeader(context, fullLength, ref offset);
                                fileStream = store.GetReadStream(string.Empty, mp4Path, (int)offset);

                                title = FileUtility.ReplaceFileExtension(title, ".mp4");
                            }
                            else
                            {
                                if (!FileConverter.EnableConvert(file, ext))
                                {
                                    if (!readLink && fileDao.IsSupportedPreSignedUri(file))
                                    {
                                        context.Response.Redirect(fileDao.GetPreSignedUri(file, TimeSpan.FromHours(1)).ToString(), true);

                                        return;
                                    }

                                    fileStream = fileDao.GetFileStream(file); // getStream to fix file.ContentLength

                                    if (fileStream.CanSeek)
                                    {
                                        var fullLength = file.ContentLength;
                                        length = ProcessRangeHeader(context, fullLength, ref offset);
                                        fileStream.Seek(offset, SeekOrigin.Begin);
                                    }
                                    else
                                    {
                                        length = file.ContentLength;
                                    }
                                }
                                else
                                {
                                    title = FileUtility.ReplaceFileExtension(title, ext);
                                    fileStream = FileConverter.Exec(file, ext);

                                    length = fileStream.Length;
                                }
                            }

                            SendStreamByChunks(context, length, title, fileStream, ref flushed);
                        }
                        else
                        {
                            if (!readLink && fileDao.IsSupportedPreSignedUri(file))
                            {
                                context.Response.Redirect(fileDao.GetPreSignedUri(file, TimeSpan.FromHours(1)).ToString(), true);

                                return;
                            }

                            fileStream = fileDao.GetFileStream(file); // getStream to fix file.ContentLength

                            long offset = 0;
                            var length = file.ContentLength;
                            if (fileStream.CanSeek)
                            {
                                length = ProcessRangeHeader(context, file.ContentLength, ref offset);
                                fileStream.Seek(offset, SeekOrigin.Begin);
                            }

                            SendStreamByChunks(context, length, title, fileStream, ref flushed);
                        }
                    }
                    catch (ThreadAbortException tae)
                    {
                        Logger.Error("DownloadFile", tae);
                    }
                    catch (HttpException e)
                    {
                        Logger.Error("DownloadFile", e);
                        throw new HttpException((int)HttpStatusCode.BadRequest, e.Message);
                    }
                    finally
                    {
                        if (fileStream != null)
                        {
                            fileStream.Close();
                            fileStream.Dispose();
                        }
                    }

                    try
                    {
                        context.Response.Body.Flush();
                        //context.Response.SuppressContent = true;
                        //context.ApplicationInstance.CompleteRequest();
                        flushed = true;
                    }
                    catch (HttpException ex)
                    {
                        Logger.Error("DownloadFile", ex);
                    }
                }
            }
            catch (ThreadAbortException tae)
            {
                Logger.Error("DownloadFile", tae);
            }
            catch (Exception ex)
            {
                // Get stack trace for the exception with source file information
                var st = new StackTrace(ex, true);
                // Get the top stack frame
                var frame = st.GetFrame(0);
                // Get the line number from the stack frame
                var line = frame.GetFileLineNumber();

                Logger.ErrorFormat("Url: {0} {1} IsClientConnected:{2}, line number:{3} frame:{4}", context.Request.Url(), ex, !context.RequestAborted.IsCancellationRequested, line, frame);
                if (!flushed && !context.RequestAborted.IsCancellationRequested)
                {
                    context.Response.StatusCode = 400;
                    context.Response.WriteAsync(HttpUtility.HtmlEncode(ex.Message)).Wait();
                }
            }
        }

        private long ProcessRangeHeader(HttpContext context, long fullLength, ref long offset)
        {
            if (context == null) throw new ArgumentNullException();
            if (context.Request.Headers["Range"].FirstOrDefault() == null) return fullLength;

            long endOffset = -1;

            var range = context.Request.Headers["Range"].FirstOrDefault().Split(new[] { '=', '-' });
            offset = Convert.ToInt64(range[1]);
            if (range.Count() > 2 && !string.IsNullOrEmpty(range[2]))
            {
                endOffset = Convert.ToInt64(range[2]);
            }
            if (endOffset < 0 || endOffset >= fullLength)
            {
                endOffset = fullLength - 1;
            }

            var length = endOffset - offset + 1;

            if (length <= 0) throw new HttpException(HttpStatusCode.RequestedRangeNotSatisfiable);

            Logger.InfoFormat("Starting file download (chunk {0}-{1})", offset, endOffset);
            if (length < fullLength)
            {
                context.Response.StatusCode = (int)HttpStatusCode.PartialContent;
            }
            context.Response.Headers.Add("Accept-Ranges", "bytes");
            context.Response.Headers.Add("Content-Range", string.Format(" bytes {0}-{1}/{2}", offset, endOffset, fullLength));

            return length;
        }

        private void SendStreamByChunks(HttpContext context, long toRead, string title, Stream fileStream, ref bool flushed)
        {
            //context.Response.Buffer = false;
            context.Response.Headers.Add("Connection", "Keep-Alive");
            context.Response.Headers.Add("Content-Length", toRead.ToString(CultureInfo.InvariantCulture));
            context.Response.Headers.Add("Content-Disposition", ContentDispositionUtil.GetHeaderValue(title));
            context.Response.ContentType = MimeMapping.GetMimeMapping(title);

            const int bufferSize = 32 * 1024; // 32KB
            var buffer = new byte[bufferSize];
            while (toRead > 0)
            {
                var length = fileStream.Read(buffer, 0, bufferSize);

                if (!context.RequestAborted.IsCancellationRequested)
                {
                    context.Response.Body.Write(buffer, 0, length);
                    context.Response.Body.Flush();
                    flushed = true;
                    toRead -= length;
                }
                else
                {
                    toRead = -1;
                    Logger.Warn(string.Format("IsClientConnected is false. Why? Download file {0} Connection is lost. ", title));
                }
            }
        }

        private void StreamFile(HttpContext context)
        {
            try
            {
                var fileDao = DaoFactory.FileDao;
                var id = context.Request.Query[FilesLinkUtility.FileId];
                if (!int.TryParse(context.Request.Query[FilesLinkUtility.Version].FirstOrDefault() ?? "", out var version))
                {
                    version = 0;
                }
                var doc = context.Request.Query[FilesLinkUtility.DocShareKey];

                fileDao.InvalidateCache(id);

                var linkRight = FileShareLink.Check(doc, fileDao, out var file);
                if (linkRight == FileShare.Restrict && !SecurityContext.IsAuthenticated)
                {
                    var auth = context.Request.Query[FilesLinkUtility.AuthKey];
                    var validateResult = EmailValidationKeyProvider.ValidateEmailKey(id + version, auth.FirstOrDefault() ?? "", Global.StreamUrlExpire);
                    if (validateResult != EmailValidationKeyProvider.ValidationResult.Ok)
                    {
                        var exc = new HttpException((int)HttpStatusCode.Forbidden, FilesCommonResource.ErrorMassage_SecurityException);

                        Logger.Error($"{FilesLinkUtility.AuthKey} {validateResult}: {context.Request.Url()}", exc);

                        context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                        context.Response.WriteAsync(FilesCommonResource.ErrorMassage_SecurityException).Wait();
                        return;
                    }

                    if (!string.IsNullOrEmpty(FileUtility.SignatureSecret))
                    {
                        try
                        {
                            var header = context.Request.Headers[FileUtility.SignatureHeader].FirstOrDefault();
                            if (string.IsNullOrEmpty(header) || !header.StartsWith("Bearer "))
                            {
                                throw new Exception("Invalid header " + header);
                            }
                            header = header.Substring("Bearer ".Length);

                            JsonWebToken.JsonSerializer = new DocumentService.JwtSerializer();

                            var stringPayload = JsonWebToken.Decode(header, FileUtility.SignatureSecret);

                            Logger.Debug("DocService StreamFile payload: " + stringPayload);
                            //var data = JObject.Parse(stringPayload);
                            //if (data == null)
                            //{
                            //    throw new ArgumentException("DocService StreamFile header is incorrect");
                            //}

                            //var signedStringUrl = data["url"] ?? (data["payload"] != null ? data["payload"]["url"] : null);
                            //if (signedStringUrl == null)
                            //{
                            //    throw new ArgumentException("DocService StreamFile header url is incorrect");
                            //}
                            //var signedUrl = new Uri(signedStringUrl.ToString());

                            //var signedQuery = signedUrl.Query;
                            //if (!context.Request.Url.Query.Equals(signedQuery))
                            //{
                            //    throw new SecurityException(string.Format("DocService StreamFile header id not equals: {0} and {1}", context.Request.Url.Query, signedQuery));
                            //}
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Download stream header " + context.Request.Url(), ex);
                            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                            context.Response.WriteAsync(FilesCommonResource.ErrorMassage_SecurityException).Wait();
                            return;
                        }
                    }
                }

                if (file == null
                    || version > 0 && file.Version != version)
                {
                    file = version > 0
                               ? fileDao.GetFile(id, version)
                               : fileDao.GetFile(id);
                }

                if (file == null)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    return;
                }

                if (linkRight == FileShare.Restrict && SecurityContext.IsAuthenticated && !FileSecurity.CanRead(file))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    return;
                }

                if (!string.IsNullOrEmpty(file.Error))
                {
                    context.Response.WriteAsync(file.Error).Wait();
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    return;
                }

                context.Response.Headers.Add("Content-Disposition", ContentDispositionUtil.GetHeaderValue(file.Title));
                context.Response.ContentType = MimeMapping.GetMimeMapping(file.Title);

                using (var stream = fileDao.GetFileStream(file))
                {
                    context.Response.Headers.Add("Content-Length",
                                               stream.CanSeek
                                                   ? stream.Length.ToString(CultureInfo.InvariantCulture)
                                                   : file.ContentLength.ToString(CultureInfo.InvariantCulture));
                    stream.StreamCopyTo(context.Response.Body);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error for: " + context.Request.Url(), ex);
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.WriteAsync(ex.Message).Wait();
                return;
            }

            try
            {
                context.Response.Body.Flush();
                //context.Response.SuppressContent = true;
                //context.ApplicationInstance.CompleteRequest();
            }
            catch (HttpException he)
            {
                Logger.ErrorFormat("StreamFile", he);
            }
        }

        private void EmptyFile(HttpContext context)
        {
            try
            {
                var fileName = context.Request.Query[FilesLinkUtility.FileTitle];
                if (!string.IsNullOrEmpty(FileUtility.SignatureSecret))
                {
                    try
                    {
                        var header = context.Request.Headers[FileUtility.SignatureHeader].FirstOrDefault();
                        if (string.IsNullOrEmpty(header) || !header.StartsWith("Bearer "))
                        {
                            throw new Exception("Invalid header " + header);
                        }
                        header = header.Substring("Bearer ".Length);

                        JsonWebToken.JsonSerializer = new DocumentService.JwtSerializer();

                        var stringPayload = JsonWebToken.Decode(header, FileUtility.SignatureSecret);

                        Logger.Debug("DocService EmptyFile payload: " + stringPayload);
                        //var data = JObject.Parse(stringPayload);
                        //if (data == null)
                        //{
                        //    throw new ArgumentException("DocService EmptyFile header is incorrect");
                        //}

                        //var signedStringUrl = data["url"] ?? (data["payload"] != null ? data["payload"]["url"] : null);
                        //if (signedStringUrl == null)
                        //{
                        //    throw new ArgumentException("DocService EmptyFile header url is incorrect");
                        //}
                        //var signedUrl = new Uri(signedStringUrl.ToString());

                        //var signedQuery = signedUrl.Query;
                        //if (!context.Request.Url.Query.Equals(signedQuery))
                        //{
                        //    throw new SecurityException(string.Format("DocService EmptyFile header id not equals: {0} and {1}", context.Request.Url.Query, signedQuery));
                        //}
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Download stream header " + context.Request.Url(), ex);
                        context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                        context.Response.WriteAsync(FilesCommonResource.ErrorMassage_SecurityException).Wait();
                        return;
                    }
                }

                var toExtension = FileUtility.GetFileExtension(fileName);
                var fileExtension = FileUtility.GetInternalExtension(toExtension);
                fileName = "new" + fileExtension;
                var path = FileConstant.NewDocPath
                           + (CoreBaseSettings.CustomMode ? "ru-RU/" : "default/")
                           + fileName;

                var storeTemplate = GlobalStore.GetStoreTemplate();
                if (!storeTemplate.IsFile("", path))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    context.Response.WriteAsync(FilesCommonResource.ErrorMassage_FileNotFound).Wait();
                    return;
                }

                context.Response.Headers.Add("Content-Disposition", ContentDispositionUtil.GetHeaderValue(fileName));
                context.Response.ContentType = MimeMapping.GetMimeMapping(fileName);

                using var stream = storeTemplate.GetReadStream("", path);
                context.Response.Headers.Add("Content-Length",
                    stream.CanSeek
                    ? stream.Length.ToString(CultureInfo.InvariantCulture)
                    : storeTemplate.GetFileSize("", path).ToString(CultureInfo.InvariantCulture));
                stream.StreamCopyTo(context.Response.Body);
            }
            catch (Exception ex)
            {
                Logger.Error("Error for: " + context.Request.Url(), ex);
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.WriteAsync(ex.Message).Wait();
                return;
            }

            try
            {
                context.Response.Body.Flush();
                //context.Response.SuppressContent = true;
                //context.ApplicationInstance.CompleteRequest();
            }
            catch (HttpException he)
            {
                Logger.ErrorFormat("EmptyFile", he);
            }
        }

        private void TempFile(HttpContext context)
        {
            var fileName = context.Request.Query[FilesLinkUtility.FileTitle];
            var auth = context.Request.Query[FilesLinkUtility.AuthKey].FirstOrDefault();

            var validateResult = EmailValidationKeyProvider.ValidateEmailKey(fileName, auth ?? "", Global.StreamUrlExpire);
            if (validateResult != EmailValidationKeyProvider.ValidationResult.Ok)
            {
                var exc = new HttpException((int)HttpStatusCode.Forbidden, FilesCommonResource.ErrorMassage_SecurityException);

                Logger.Error($"{FilesLinkUtility.AuthKey} {validateResult}: {context.Request.Url()}", exc);

                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                context.Response.WriteAsync(FilesCommonResource.ErrorMassage_SecurityException);
                return;
            }

            context.Response.Clear();
            context.Response.ContentType = MimeMapping.GetMimeMapping(fileName);
            context.Response.Headers.Add("Content-Disposition", ContentDispositionUtil.GetHeaderValue(fileName));

            var store = GlobalStore.GetStore();

            var path = Path.Combine("temp_stream", fileName);

            if (!store.IsFile(FileConstant.StorageDomainTmp, path))
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                context.Response.WriteAsync(FilesCommonResource.ErrorMassage_FileNotFound).Wait();
                return;
            }

            using (var readStream = store.GetReadStream(FileConstant.StorageDomainTmp, path))
            {
                context.Response.Headers.Add("Content-Length", readStream.Length.ToString(CultureInfo.InvariantCulture));
                readStream.StreamCopyTo(context.Response.Body);
            }

            store.Delete(FileConstant.StorageDomainTmp, path);

            try
            {
                context.Response.Body.Flush();
                //context.Response.SuppressContent = true;
                //context.ApplicationInstance.CompleteRequest();
            }
            catch (HttpException he)
            {
                Logger.ErrorFormat("TempFile", he);
            }
        }

        private void DifferenceFile(HttpContext context)
        {
            try
            {
                var fileDao = DaoFactory.FileDao;
                var id = context.Request.Query[FilesLinkUtility.FileId];
                int.TryParse(context.Request.Query[FilesLinkUtility.Version].FirstOrDefault() ?? "", out var version);
                var doc = context.Request.Query[FilesLinkUtility.DocShareKey];

                var linkRight = FileShareLink.Check(doc, fileDao, out var file);
                if (linkRight == FileShare.Restrict && !SecurityContext.IsAuthenticated)
                {
                    var auth = context.Request.Query[FilesLinkUtility.AuthKey].FirstOrDefault();
                    var validateResult = EmailValidationKeyProvider.ValidateEmailKey(id + version, auth ?? "", Global.StreamUrlExpire);
                    if (validateResult != EmailValidationKeyProvider.ValidationResult.Ok)
                    {
                        var exc = new HttpException((int)HttpStatusCode.Forbidden, FilesCommonResource.ErrorMassage_SecurityException);

                        Logger.Error($"{FilesLinkUtility.AuthKey} {validateResult}: {context.Request.Url()}", exc);

                        context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                        context.Response.WriteAsync(FilesCommonResource.ErrorMassage_SecurityException).Wait();
                        return;
                    }
                }

                fileDao.InvalidateCache(id);

                if (file == null
                    || version > 0 && file.Version != version)
                {
                    file = version > 0
                               ? fileDao.GetFile(id, version)
                               : fileDao.GetFile(id);
                }

                if (file == null)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    return;
                }

                if (linkRight == FileShare.Restrict && SecurityContext.IsAuthenticated && !FileSecurity.CanRead(file))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    return;
                }

                if (!string.IsNullOrEmpty(file.Error))
                {
                    context.Response.WriteAsync(file.Error).Wait();
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    return;
                }

                context.Response.Headers.Add("Content-Disposition", ContentDispositionUtil.GetHeaderValue(".zip"));
                context.Response.ContentType = MimeMapping.GetMimeMapping(".zip");

                using (var stream = fileDao.GetDifferenceStream(file))
                {
                    context.Response.Headers.Add("Content-Length", stream.Length.ToString(CultureInfo.InvariantCulture));
                    stream.StreamCopyTo(context.Response.Body);
                }
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.WriteAsync(ex.Message).Wait();
                Logger.Error("Error for: " + context.Request.Url(), ex);
                return;
            }

            try
            {
                context.Response.Body.Flush();
                //context.Response.SuppressContent = true;
                //context.ApplicationInstance.CompleteRequest();
            }
            catch (HttpException he)
            {
                Logger.ErrorFormat("DifferenceFile", he);
            }
        }

        private static string GetEtag(File file)
        {
            return file.ID + ":" + file.Version + ":" + file.Title.GetHashCode() + ":" + file.ContentLength;
        }

        private void CreateFile(HttpContext context)
        {
            var responseMessage = context.Request.Query["response"] == "message";
            var folderId = context.Request.Query[FilesLinkUtility.FolderId].FirstOrDefault();
            if (string.IsNullOrEmpty(folderId))
                folderId = GlobalFolderHelper.FolderMy.ToString();
            Folder folder;

            var folderDao = DaoFactory.FolderDao;
            folder = folderDao.GetFolder(folderId);

            if (folder == null) throw new HttpException((int)HttpStatusCode.NotFound, FilesCommonResource.ErrorMassage_FolderNotFound);
            if (!FileSecurity.CanCreate(folder)) throw new HttpException((int)HttpStatusCode.Forbidden, FilesCommonResource.ErrorMassage_SecurityException_Create);

            File file;
            var fileUri = context.Request.Query[FilesLinkUtility.FileUri];
            var fileTitle = context.Request.Query[FilesLinkUtility.FileTitle];
            try
            {
                if (!string.IsNullOrEmpty(fileUri))
                {
                    file = CreateFileFromUri(folder, fileUri, fileTitle);
                }
                else
                {
                    var docType = context.Request.Query["doctype"];
                    file = CreateFileFromTemplate(folder, fileTitle, docType);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                if (responseMessage)
                {
                    context.Response.WriteAsync("error: " + ex.Message).Wait();
                    return;
                }
                context.Response.Redirect(PathProvider.StartURL + "#error/" + HttpUtility.UrlEncode(ex.Message), true);
                return;
            }

            FileMarker.MarkAsNew(file);

            if (responseMessage)
            {
                context.Response.WriteAsync("ok: " + string.Format(FilesCommonResource.MessageFileCreated, folder.Title)).Wait();
                return;
            }

            context.Response.Redirect(
                (context.Request.Query["openfolder"].FirstOrDefault() ?? "").Equals("true")
                    ? PathProvider.GetFolderUrl(file.FolderID)
                    : (FilesLinkUtility.GetFileWebEditorUrl(file.ID) + "#message/" + HttpUtility.UrlEncode(string.Format(FilesCommonResource.MessageFileCreated, folder.Title))));
        }

        private File CreateFileFromTemplate(Folder folder, string fileTitle, string docType)
        {
            var storeTemplate = GlobalStore.GetStoreTemplate();

            var lang = UserManager.GetUsers(SecurityContext.CurrentAccount.ID).GetCulture();

            var fileExt = FileUtility.InternalExtension[FileType.Document];
            if (!string.IsNullOrEmpty(docType))
            {
                var tmpFileType = Services.DocumentService.Configuration.DocType.FirstOrDefault(r => r.Value.Equals(docType, StringComparison.OrdinalIgnoreCase));
                FileUtility.InternalExtension.TryGetValue(tmpFileType.Key, out var tmpFileExt);
                if (!string.IsNullOrEmpty(tmpFileExt))
                    fileExt = tmpFileExt;
            }

            var templateName = "new" + fileExt;

            var templatePath = FileConstant.NewDocPath + lang + "/";
            if (!storeTemplate.IsDirectory(templatePath))
                templatePath = FileConstant.NewDocPath + "default/";
            templatePath += templateName;

            if (string.IsNullOrEmpty(fileTitle))
            {
                fileTitle = templateName;
            }
            else
            {
                fileTitle = fileTitle + fileExt;
            }

            var file = new File
            {
                Title = fileTitle,
                FolderID = folder.ID,
                Comment = FilesCommonResource.CommentCreate,
            };

            var fileDao = DaoFactory.FileDao;
            var stream = storeTemplate.GetReadStream("", templatePath);
            file.ContentLength = stream.CanSeek ? stream.Length : storeTemplate.GetFileSize(templatePath);
            return fileDao.SaveFile(file, stream);
        }

        private File CreateFileFromUri(Folder folder, string fileUri, string fileTitle)
        {
            if (string.IsNullOrEmpty(fileTitle))
                fileTitle = Path.GetFileName(HttpUtility.UrlDecode(fileUri));

            var file = new File
            {
                Title = fileTitle,
                FolderID = folder.ID,
                Comment = FilesCommonResource.CommentCreate,
            };

            var req = (HttpWebRequest)WebRequest.Create(fileUri);

            // hack. http://ubuntuforums.org/showthread.php?t=1841740
            if (WorkContext.IsMono)
            {
                ServicePointManager.ServerCertificateValidationCallback += (s, ce, ca, p) => true;
            }

            var fileDao = DaoFactory.FileDao;
            var fileStream = new ResponseStream(req.GetResponse());
            file.ContentLength = fileStream.Length;

            return fileDao.SaveFile(file, fileStream);
        }

        private void Redirect(HttpContext context)
        {
            if (!SecurityContext.AuthenticateMe(CookiesManager.GetCookies(CookiesType.AuthKey)))
            {
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                return;
            }
            var urlRedirect = string.Empty;
            var folderId = context.Request.Query[FilesLinkUtility.FolderId];
            if (!string.IsNullOrEmpty(folderId))
            {
                try
                {
                    urlRedirect = PathProvider.GetFolderUrl(folderId);
                }
                catch (ArgumentNullException e)
                {
                    throw new HttpException((int)HttpStatusCode.BadRequest, e.Message);
                }
            }

            var fileId = context.Request.Query[FilesLinkUtility.FileId];
            if (!string.IsNullOrEmpty(fileId))
            {
                var fileDao = DaoFactory.FileDao;
                var file = fileDao.GetFile(fileId);
                if (file == null)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    return;
                }

                urlRedirect = FilesLinkUtility.GetFileWebPreviewUrl(FileUtility, file.Title, file.ID);
            }

            if (string.IsNullOrEmpty(urlRedirect))
                throw new HttpException((int)HttpStatusCode.BadRequest, FilesCommonResource.ErrorMassage_BadRequest);
            context.Response.Redirect(urlRedirect);
        }

        private void TrackFile(HttpContext context)
        {
            var auth = context.Request.Query[FilesLinkUtility.AuthKey].FirstOrDefault();
            var fileId = context.Request.Query[FilesLinkUtility.FileId].FirstOrDefault();
            Logger.Debug("DocService track fileid: " + fileId);

            var callbackSpan = TimeSpan.FromDays(128);
            var validateResult = EmailValidationKeyProvider.ValidateEmailKey(fileId, auth ?? "", callbackSpan);
            if (validateResult != EmailValidationKeyProvider.ValidationResult.Ok)
            {
                Logger.ErrorFormat("DocService track auth error: {0}, {1}: {2}", validateResult.ToString(), FilesLinkUtility.AuthKey, auth);
                throw new HttpException((int)HttpStatusCode.Forbidden, FilesCommonResource.ErrorMassage_SecurityException);
            }

            DocumentServiceTracker.TrackerData fileData;
            try
            {
                string body;
                var receiveStream = context.Request.Body;
                var readStream = new StreamReader(receiveStream);
                body = readStream.ReadToEnd();

                Logger.Debug("DocService track body: " + body);
                if (string.IsNullOrEmpty(body))
                {
                    throw new ArgumentException("DocService request body is incorrect");
                }

                var data = JToken.Parse(body);
                if (data == null)
                {
                    throw new ArgumentException("DocService request is incorrect");
                }
                fileData = data.ToObject<DocumentServiceTracker.TrackerData>();
            }
            catch (Exception e)
            {
                Logger.Error("DocService track error read body", e);
                throw new HttpException((int)HttpStatusCode.BadRequest, e.Message);
            }

            if (!string.IsNullOrEmpty(FileUtility.SignatureSecret))
            {
                JsonWebToken.JsonSerializer = new DocumentService.JwtSerializer();
                if (!string.IsNullOrEmpty(fileData.Token))
                {
                    try
                    {
                        var dataString = JsonWebToken.Decode(fileData.Token, FileUtility.SignatureSecret);
                        var data = JObject.Parse(dataString);
                        if (data == null)
                        {
                            throw new ArgumentException("DocService request token is incorrect");
                        }
                        fileData = data.ToObject<DocumentServiceTracker.TrackerData>();
                    }
                    catch (SignatureVerificationException ex)
                    {
                        Logger.Error("DocService track header", ex);
                        throw new HttpException((int)HttpStatusCode.Forbidden, ex.Message);
                    }
                }
                else
                {
                    //todo: remove old scheme
                    var header = context.Request.Headers[FileUtility.SignatureHeader].FirstOrDefault();
                    if (string.IsNullOrEmpty(header) || !header.StartsWith("Bearer "))
                    {
                        Logger.Error("DocService track header is null");
                        throw new HttpException((int)HttpStatusCode.Forbidden, FilesCommonResource.ErrorMassage_SecurityException);
                    }
                    header = header.Substring("Bearer ".Length);

                    try
                    {
                        var stringPayload = JsonWebToken.Decode(header, FileUtility.SignatureSecret);

                        Logger.Debug("DocService track payload: " + stringPayload);
                        var jsonPayload = JObject.Parse(stringPayload);
                        var data = jsonPayload["payload"];
                        if (data == null)
                        {
                            throw new ArgumentException("DocService request header is incorrect");
                        }
                        fileData = data.ToObject<DocumentServiceTracker.TrackerData>();
                    }
                    catch (SignatureVerificationException ex)
                    {
                        Logger.Error("DocService track header", ex);
                        throw new HttpException((int)HttpStatusCode.Forbidden, ex.Message);
                    }
                }
            }

            DocumentServiceTracker.TrackResponse result;
            try
            {
                result = DocumentServiceTrackerHelper.ProcessData(fileId, fileData);
            }
            catch (Exception e)
            {
                Logger.Error("DocService track:", e);
                throw new HttpException((int)HttpStatusCode.BadRequest, e.Message);
            }
            result ??= new DocumentServiceTracker.TrackResponse();

            context.Response.WriteAsync(DocumentServiceTracker.TrackResponse.Serialize(result)).Wait();
        }
    }
}