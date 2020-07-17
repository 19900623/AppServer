﻿using System.Text.Json.Serialization;

using ASC.Api.Core.Auth;
using ASC.Api.Core.Core;
using ASC.Api.Core.Middleware;
using ASC.Common;
using ASC.Common.DependencyInjection;
using ASC.Common.Logging;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ASC.Api.Core
{
    public abstract class BaseStartup
    {
        public IConfiguration Configuration { get; }
        public IHostEnvironment HostEnvironment { get; }
        public virtual string[] LogParams { get; }
        public virtual JsonConverter[] Converters { get; }
        public virtual bool AddControllers { get; } = true;
        public virtual bool ConfirmAddScheme { get; } = false;

        public BaseStartup(IConfiguration configuration, IHostEnvironment hostEnvironment)
        {
            Configuration = configuration;
            HostEnvironment = hostEnvironment;
        }

        public virtual void ConfigureServices(IServiceCollection services)
        {
            services.AddHttpContextAccessor();

            var diHelper = new DIHelper(services);

            if (AddControllers)
            {
                services.AddControllers()
                    .AddXmlSerializerFormatters()
                    .AddJsonOptions(options =>
                    {
                        options.JsonSerializerOptions.WriteIndented = false;
                        options.JsonSerializerOptions.IgnoreNullValues = true;
                        options.JsonSerializerOptions.Converters.Add(new ApiDateTimeConverter());

                        if (Converters != null)
                        {
                            foreach (var c in Converters)
                            {
                                options.JsonSerializerOptions.Converters.Add(c);
                            }
                        }
                    });
            }

            diHelper
                .AddCultureMiddleware()
                .AddIpSecurityFilter()
                .AddPaymentFilter()
                .AddProductSecurityFilter()
                .AddTenantStatusFilter();

            var builder = services.AddMvcCore(config =>
            {
                var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
                config.Filters.Add(new AuthorizeFilter(policy));
                config.Filters.Add(new TypeFilterAttribute(typeof(TenantStatusFilter)));
                config.Filters.Add(new TypeFilterAttribute(typeof(PaymentFilter)));
                config.Filters.Add(new TypeFilterAttribute(typeof(IpSecurityFilter)));
                config.Filters.Add(new TypeFilterAttribute(typeof(ProductSecurityFilter)));
                config.Filters.Add(new CustomResponseFilterAttribute());
                config.Filters.Add(new CustomExceptionFilterAttribute());
                config.Filters.Add(new TypeFilterAttribute(typeof(FormatFilter)));

                config.OutputFormatters.RemoveType<XmlSerializerOutputFormatter>();
                config.OutputFormatters.Add(new XmlOutputFormatter());
            });

            diHelper.AddCookieAuthHandler();
            var authBuilder = services.AddAuthentication("cookie")
                .AddScheme<AuthenticationSchemeOptions, CookieAuthHandler>("cookie", a => { });

            if (ConfirmAddScheme)
            {
                authBuilder.AddScheme<AuthenticationSchemeOptions, ConfirmAuthHandler>("confirm", a => { });
            }

            if (LogParams != null)
            {
                diHelper.AddNLogManager(LogParams);
            }

            services.AddAutofac(Configuration, HostEnvironment.ContentRootPath);
        }

        public virtual void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            app.UseRouting();

            app.UseAuthentication();

            app.UseAuthorization();

            app.UseCultureMiddleware();

            app.UseDisposeMiddleware();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapCustom();
            });
        }
    }
}