﻿using System.Collections.Generic;
using ASC.Core;
using ASC.Web.Api.Routing;
using ASC.Web.Core;
using ASC.Web.Core.WebZones;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ASC.Web.Api.Controllers
{
    [DefaultRoute]
    [ApiController]
    public class ModulesController : ControllerBase
    {
        public UserManager UserManager { get; }
        public TenantManager TenantManager { get; }
        public WebItemManagerSecurity WebItemManagerSecurity { get; }

        public ModulesController(
            UserManager userManager,
            TenantManager tenantManager,
            WebItemManagerSecurity webItemManagerSecurity)
        {
            UserManager = userManager;
            TenantManager = tenantManager;
            WebItemManagerSecurity = webItemManagerSecurity;
        }

        [Read]
        public IEnumerable<string> GetAll()
        {
            var result = new List<string>();

            foreach (var a in WebItemManagerSecurity.GetItems(WebZoneType.StartProductList))
            {
                result.Add(a.ApiURL);
            }

            return result;
        }
    }

    public static class ModulesControllerExtension
    {
        public static IServiceCollection AddModulesController(this IServiceCollection services)
        {
            return services
                .AddUserManagerService()
                .AddTenantManagerService()
                .AddWebItemManagerSecurity();
        }
    }
}
