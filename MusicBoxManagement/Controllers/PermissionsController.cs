using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using MusicBoxManagement.Authorization;
using MusicBoxManagement.Models;
using MusicBoxManagement.Services;

namespace MusicBoxManagement.Controllers
{
    [PermissionAuthorize("Permission.Manage")]
    public class PermissionsController : Controller
    {
        public ActionResult Index()
        {
            using (var db = new ApplicationDbContext())
                return View(new PermissionMatrixService(db).GetRows());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult SetGrant(string role, string code, bool enabled)
        {
            using (var db = new ApplicationDbContext())
            {
                var result = new PermissionMatrixService(db)
                    .SetGrant(role, code, enabled, User.Identity.GetUserId());
                TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
                    ? "Đã cập nhật quyền " + code + " của " + role + "." : result.Error;
                return RedirectToAction("Index");
            }
        }
    }
}
