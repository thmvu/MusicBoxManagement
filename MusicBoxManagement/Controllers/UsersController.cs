using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using MusicBoxManagement.Authorization;
using MusicBoxManagement.Models;
using MusicBoxManagement.Services;

namespace MusicBoxManagement.Controllers
{
    [PermissionAuthorize("User.Manage")]
    public class UsersController : Controller
    {
        private UserManagementService Service
        {
            get
            {
                var context = HttpContext.GetOwinContext();
                return new UserManagementService(
                    context.Get<ApplicationDbContext>(),
                    context.GetUserManager<ApplicationUserManager>());
            }
        }

        public ActionResult Index()
        {
            return View(Service.List());
        }

        public ActionResult Create()
        {
            SetRoles();
            return View(new CreateUserViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(CreateUserViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await Service.CreateAsync(model, User.Identity.GetUserId());
                if (result.Succeeded)
                {
                    TempData["Success"] = "Đã tạo tài khoản.";
                    return RedirectToAction("Index");
                }
                foreach (var error in result.Errors) ModelState.AddModelError("", error);
            }
            SetRoles(model.Role);
            return View(model);
        }

        public ActionResult Edit(string id)
        {
            var model = Service.GetEdit(id);
            if (model == null) return HttpNotFound();
            SetRoles(model.Role);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(EditUserViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await Service.EditAsync(model, User.Identity.GetUserId());
                if (result.Succeeded)
                {
                    TempData["Success"] = "Đã cập nhật tài khoản.";
                    return RedirectToAction("Index");
                }
                foreach (var error in result.Errors) ModelState.AddModelError("", error);
            }
            SetRoles(model.Role);
            return View(model);
        }

        public ActionResult ResetPassword(string id)
        {
            var user = Service.GetEdit(id);
            if (user == null) return HttpNotFound();
            return View(new ResetUserPasswordViewModel { Id = user.Id, UserName = user.UserName });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ResetPassword(ResetUserPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await Service.ResetPasswordAsync(model.Id, model.NewPassword, User.Identity.GetUserId());
                if (result.Succeeded)
                {
                    TempData["Success"] = "Đã đặt lại mật khẩu.";
                    return RedirectToAction("Index");
                }
                foreach (var error in result.Errors) ModelState.AddModelError("", error);
            }
            var user = Service.GetEdit(model.Id);
            if (user == null) return HttpNotFound();
            model.UserName = user.UserName;
            return View(model);
        }

        private void SetRoles(string selected = null)
        {
            ViewBag.Roles = new SelectList(new[] { "Staff", "Manager", "Admin" }, selected);
        }
    }
}
