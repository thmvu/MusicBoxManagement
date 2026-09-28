using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using MusicBoxManagement.Authorization;
using MusicBoxManagement.Models;
using MusicBoxManagement.Services;

namespace MusicBoxManagement.Controllers
{
    public class OrdersController : Controller
    {
        [PermissionAuthorize("Order.View")]
        public ActionResult Index(int? sessionId)
        {
            using (var db = new ApplicationDbContext())
            {
                ViewBag.SessionId = sessionId;
                ViewBag.CanCreate = Has(db, "Order.Create");
                ViewBag.CanConfirm = Has(db, "Order.Confirm");
                ViewBag.CanCancel = Has(db, "Order.Cancel");
                var reader = new OrderReadService(db);
                return View(sessionId.HasValue ? reader.ForSession(sessionId.Value) : reader.PendingQueue());
            }
        }

        [PermissionAuthorize("Order.View")]
        public ActionResult Details(int id)
        {
            using (var db = new ApplicationDbContext())
            {
                var item = new OrderReadService(db).Find(id);
                if (item == null) return HttpNotFound();
                ViewBag.CanConfirm = Has(db, "Order.Confirm");
                ViewBag.CanCancel = Has(db, "Order.Cancel");
                ViewBag.SessionActive = db.RoomSessions.AsNoTracking().Any(session =>
                    session.RoomSessionId == item.RoomSessionId && session.Status == RoomSessionStatuses.Active);
                return View(item);
            }
        }

        [PermissionAuthorize("Order.Create")]
        public ActionResult Create(int sessionId)
        {
            using (var db = new ApplicationDbContext())
            {
                var model = BuildCreate(db, sessionId);
                if (model == null) return HttpNotFound();
                return View(model);
            }
        }

        [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("Order.Create")]
        public ActionResult Create(StaffOrderCreateViewModel model)
        {
            if (model == null) return new HttpStatusCodeResult(400);
            using (var db = new ApplicationDbContext())
            {
                var selected = (model.Lines ?? new List<OrderLineChoiceViewModel>())
                    .Where(line => line.Quantity != 0)
                    .Select(line => new OrderLineInput { ServiceId = line.ServiceId, Quantity = line.Quantity });
                var result = new OrderService(db, new SystemClock())
                    .CreateByStaff(model.RoomSessionId, User.Identity.GetUserId(), selected);
                if (result.Succeeded)
                {
                    TempData["Success"] = "Đã ghi món đã phục vụ vào đơn #" + result.OrderId + ".";
                    return RedirectToAction("Details", new { id = result.OrderId });
                }
                ModelState.AddModelError("", result.Error);
                var refreshed = BuildCreate(db, model.RoomSessionId);
                if (refreshed == null) return HttpNotFound();
                foreach (var line in refreshed.Lines)
                    line.Quantity = model.Lines?.FirstOrDefault(input => input.ServiceId == line.ServiceId)?.Quantity ?? 0;
                return View(refreshed);
            }
        }

        [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("Order.Confirm")]
        public ActionResult Confirm(int id)
        {
            using (var db = new ApplicationDbContext())
            {
                var result = new OrderService(db, new SystemClock()).ConfirmByStaff(id, User.Identity.GetUserId());
                TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
                    ? "Đã xác nhận món đã phục vụ." : result.Error;
            }
            return RedirectToAction("Details", new { id });
        }

        [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("Order.Cancel")]
        public ActionResult Cancel(int id)
        {
            using (var db = new ApplicationDbContext())
            {
                var result = new OrderService(db, new SystemClock()).CancelByStaff(id, User.Identity.GetUserId());
                TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
                    ? "Đã hủy đơn món." : result.Error;
            }
            return RedirectToAction("Details", new { id });
        }

        private bool Has(ApplicationDbContext db, string permission)
        {
            return new PermissionService(db).HasPermission(User.Identity.GetUserId(), permission);
        }

        private static StaffOrderCreateViewModel BuildCreate(ApplicationDbContext db, int sessionId)
        {
            var model = db.RoomSessions.AsNoTracking()
                .Where(item => item.RoomSessionId == sessionId && item.Status == RoomSessionStatuses.Active)
                .Select(item => new StaffOrderCreateViewModel
                {
                    RoomSessionId = item.RoomSessionId,
                    RoomName = item.Room.Name,
                    CustomerName = item.Customer.FullName
                }).SingleOrDefault();
            if (model == null) return null;
            model.Lines = db.Services.AsNoTracking().Where(item => item.IsActive)
                .OrderBy(item => item.Category).ThenBy(item => item.Name)
                .Select(item => new OrderLineChoiceViewModel
                { ServiceId = item.ServiceId, Name = item.Name, Category = item.Category, Price = item.Price })
                .ToList();
            return model;
        }
    }
}
