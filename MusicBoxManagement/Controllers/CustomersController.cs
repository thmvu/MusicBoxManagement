using System;
using System.Data.Entity.Infrastructure;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using MusicBoxManagement.Authorization;
using MusicBoxManagement.Models;
using MusicBoxManagement.Services;

namespace MusicBoxManagement.Controllers
{
    public class CustomersController : Controller
    {
        [PermissionAuthorize("Customer.View")]
        public ActionResult Index(string search)
        {
            using (var db = new ApplicationDbContext())
            {
                ViewBag.Search = search;
                ViewBag.CanCreate = new PermissionService(db).HasPermission(User.Identity.GetUserId(), "Customer.Create");
                ViewBag.CanEdit = new PermissionService(db).HasPermission(User.Identity.GetUserId(), "Customer.Edit");
                return View(new CustomerService(db).List(search));
            }
        }

        [PermissionAuthorize("Customer.Create")]
        public ActionResult Create()
        {
            return View(new CustomerFormViewModel());
        }

        [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("Customer.Create")]
        public ActionResult Create(CustomerFormViewModel model)
        {
            if (ModelState.IsValid)
            {
                using (var db = new ApplicationDbContext())
                {
                    try
                    {
                        new CustomerService(db).Create(model, User.Identity.GetUserId());
                        TempData["Success"] = "Đã tạo khách hàng.";
                        return new PermissionService(db).HasPermission(User.Identity.GetUserId(), "Customer.View")
                            ? RedirectToAction("Index")
                            : RedirectToAction("Create");
                    }
                    catch (ArgumentException error) { ModelState.AddModelError("", error.Message); }
                    catch (DbUpdateException) { ModelState.AddModelError("PhoneNumber", "Số điện thoại đã có khách hàng."); }
                }
            }
            return View(model);
        }

        [PermissionAuthorize("Customer.Edit")]
        public ActionResult Edit(int id)
        {
            using (var db = new ApplicationDbContext())
            {
                var model = new CustomerService(db).GetForm(id);
                return model == null ? (ActionResult)HttpNotFound() : View(model);
            }
        }

        [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("Customer.Edit")]
        public ActionResult Edit(CustomerFormViewModel model)
        {
            if (ModelState.IsValid)
            {
                using (var db = new ApplicationDbContext())
                {
                    try
                    {
                        if (!new CustomerService(db).Update(model, User.Identity.GetUserId())) return HttpNotFound();
                        TempData["Success"] = "Đã cập nhật khách hàng.";
                        return new PermissionService(db).HasPermission(User.Identity.GetUserId(), "Customer.View")
                            ? RedirectToAction("Index")
                            : RedirectToAction("Edit", new { id = model.CustomerId });
                    }
                    catch (ArgumentException error) { ModelState.AddModelError("", error.Message); }
                    catch (DbUpdateException) { ModelState.AddModelError("PhoneNumber", "Số điện thoại đã có khách hàng."); }
                }
            }
            return View(model);
        }
    }
}
