using System;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using MusicBoxManagement.Authorization;
using MusicBoxManagement.Models;
using MusicBoxManagement.Services;

namespace MusicBoxManagement.Controllers
{
    [PermissionAuthorize("Service.Manage")]
    public class ServiceCatalogController : Controller
    {
        public ActionResult Index()
        {
            using (var db = new ApplicationDbContext())
                return View(new ServiceManagementService(db).List());
        }

        public ActionResult Create()
        {
            SetCategories();
            return View(new ServiceFormViewModel { IsActive = true });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Create(ServiceFormViewModel model)
        {
            Validate(model);
            if (ModelState.IsValid)
            {
                using (var db = new ApplicationDbContext())
                {
                    try
                    {
                        new ServiceManagementService(db).Create(model, User.Identity.GetUserId());
                        TempData["Success"] = "Đã tạo dịch vụ.";
                        return RedirectToAction("Index");
                    }
                    catch (ArgumentException error) { ModelState.AddModelError("", error.Message); }
                    catch (DbUpdateException) { ModelState.AddModelError("", "Không thể lưu dịch vụ. Vui lòng kiểm tra lại dữ liệu."); }
                }
            }
            SetCategories();
            return View(model);
        }

        public ActionResult Edit(int id)
        {
            using (var db = new ApplicationDbContext())
            {
                var model = new ServiceManagementService(db).GetForm(id);
                if (model == null) return HttpNotFound();
                SetCategories();
                return View(model);
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Edit(ServiceFormViewModel model)
        {
            Validate(model);
            if (ModelState.IsValid)
            {
                using (var db = new ApplicationDbContext())
                {
                    try
                    {
                        if (!new ServiceManagementService(db).Update(model, User.Identity.GetUserId())) return HttpNotFound();
                        TempData["Success"] = "Đã cập nhật dịch vụ.";
                        return RedirectToAction("Index");
                    }
                    catch (ArgumentException error) { ModelState.AddModelError("", error.Message); }
                    catch (DbUpdateException) { ModelState.AddModelError("", "Không thể lưu dịch vụ. Vui lòng kiểm tra lại dữ liệu."); }
                }
            }
            SetCategories();
            return View(model);
        }

        private void Validate(ServiceFormViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Name)) ModelState.AddModelError("Name", "Tên dịch vụ là bắt buộc.");
            if (!ServiceCategories.All.Contains(model.Category)) ModelState.AddModelError("Category", "Nhóm dịch vụ không hợp lệ.");
            if (model.Price != decimal.Truncate(model.Price)) ModelState.AddModelError("Price", "Giá phải là số nguyên đồng.");
        }

        private void SetCategories()
        {
            ViewBag.Categories = new SelectList(ServiceCategories.All);
        }
    }
}
