﻿using System.Collections.Generic;

using ASC.Common;
using ASC.Core;
using ASC.Web.Api.Routing;
using ASC.Web.Core;
using ASC.Web.Core.WebZones;

using Microsoft.AspNetCore.Mvc;

namespace ASC.Web.Api.Controllers
{
    [DefaultRoute]
    [ApiController]
    public class ModulesController : ControllerBase
    {
        private UserManager UserManager { get; }
        private TenantManager TenantManager { get; }
        private WebItemManagerSecurity WebItemManagerSecurity { get; }

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
        public static DIHelper AddModulesController(this DIHelper services)
        {
            return services
                .AddUserManagerService()
                .AddTenantManagerService()
                .AddWebItemManagerSecurity();
        }
    }
}
