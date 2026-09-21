using System;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using MusicBoxManagement.Models;
using MusicBoxManagement.Services;

namespace MusicBoxManagement.Authorization
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class PermissionAuthorizeAttribute : AuthorizeAttribute
    {
        private readonly string code;

        public PermissionAuthorizeAttribute(string code)
        {
            this.code = code;
        }

        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            if (!base.AuthorizeCore(httpContext)) return false;
            using (var db = new ApplicationDbContext())
                return new PermissionService(db).HasPermission(httpContext.User.Identity.GetUserId(), code);
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            if (filterContext.HttpContext.User.Identity.IsAuthenticated)
            {
                filterContext.Result = new HttpStatusCodeResult(403);
                return;
            }

            base.HandleUnauthorizedRequest(filterContext);
        }
    }
}
