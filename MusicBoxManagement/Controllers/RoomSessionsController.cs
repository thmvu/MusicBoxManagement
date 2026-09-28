using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using MusicBoxManagement.Authorization;
using MusicBoxManagement.Models;
using MusicBoxManagement.Services;

namespace MusicBoxManagement.Controllers
{
    public class RoomSessionsController : Controller
    {
        [PermissionAuthorize("Session.View")]
        public ActionResult Index()
        {
            using (var db = new ApplicationDbContext())
            {
                var items = db.RoomSessions.AsNoTracking()
                    .Where(item => item.Status == RoomSessionStatuses.Active)
                    .OrderBy(item => item.ActualStartTime)
                    .Select(item => new RoomSessionListItemViewModel
                    {
                        RoomSessionId = item.RoomSessionId,
                        RoomName = item.Room.Name,
                        CustomerName = item.Customer.FullName,
                        ActualStartTime = item.ActualStartTime,
                        FromReservation = item.ReservationId.HasValue
                    }).ToList();
                ViewBag.CanWalkIn = new PermissionService(db)
                    .HasPermission(User.Identity.GetUserId(), "Session.WalkIn");
                return View(items);
            }
        }

        [PermissionAuthorize("Session.View")]
        public ActionResult Details(int id)
        {
            using (var db = new ApplicationDbContext())
            {
                new NoShowService(db, new SystemClock()).ProcessExpired();
                var item = db.RoomSessions.AsNoTracking()
                    .Where(session => session.RoomSessionId == id)
                    .Select(session => new RoomSessionDetailsViewModel
                    {
                        RoomSessionId = session.RoomSessionId,
                        RoomName = session.Room.Name,
                        CustomerName = session.Customer.FullName,
                        PhoneNumber = session.Customer.PhoneNumber,
                        ReservationId = session.ReservationId,
                        Status = session.Status,
                        ActualStartTime = session.ActualStartTime,
                        ExpectedEndTime = session.ExpectedEndTime,
                        HourlyRate = session.HourlyRate,
                        RoomCodeSnapshot = session.RoomCodeSnapshot,
                        RoomTypeNameSnapshot = session.RoomTypeNameSnapshot
                    }).SingleOrDefault();
                if (item == null) return HttpNotFound();
                if (!item.ReservationId.HasValue && item.Status == RoomSessionStatuses.Active)
                    item.WarningEndUtc = new RoomSessionService(db, new SystemClock()).GetWalkInWarning(id);
                item.CanExtend = item.ReservationId.HasValue && item.Status == RoomSessionStatuses.Active &&
                    item.ExpectedEndTime.HasValue && DateTimeOffset.UtcNow <= item.ExpectedEndTime.Value &&
                    new PermissionService(db).HasPermission(User.Identity.GetUserId(), "Session.Extend");
                if (item.Status == RoomSessionStatuses.Active)
                    item.Billing = new BillingService(db, new SystemClock()).GetPreview(id);
                var permissions = new PermissionService(db);
                item.CanViewOrders = permissions.HasPermission(User.Identity.GetUserId(), "Order.View");
                item.CanCreateOrder = item.Status == RoomSessionStatuses.Active &&
                    permissions.HasPermission(User.Identity.GetUserId(), "Order.Create");
                item.CanCheckout = item.Status == RoomSessionStatuses.Active &&
                    permissions.HasPermission(User.Identity.GetUserId(), "Session.CheckOut");
                item.CanViewInvoice = permissions.HasPermission(User.Identity.GetUserId(), "Invoice.View");
                if (item.Status == RoomSessionStatuses.Completed && item.CanViewInvoice)
                    item.InvoiceId = db.Invoices.AsNoTracking().Where(invoice =>
                        invoice.RoomSessionId == id).Select(invoice => (int?)invoice.InvoiceId).SingleOrDefault();
                return View(item);
            }
        }

        [PermissionAuthorize("Session.WalkIn")]
        public ActionResult WalkIn()
        {
            using (var db = new ApplicationDbContext()) SetRoomOptions(db);
            return View(new WalkInFormViewModel());
        }

        [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("Session.WalkIn")]
        public ActionResult WalkIn(WalkInFormViewModel model)
        {
            if (model == null) return new HttpStatusCodeResult(400);
            using (var db = new ApplicationDbContext())
            {
                if (ModelState.IsValid)
                {
                    var result = new RoomSessionService(db, new SystemClock())
                        .WalkIn(model.RoomId, model.FullName, model.PhoneNumber, User.Identity.GetUserId());
                    if (result.Succeeded)
                    {
                        TempData["Success"] = "Đã nhận khách trực tiếp. Phiên #" + result.RoomSessionId + " đang hoạt động.";
                        if (new PermissionService(db).HasPermission(User.Identity.GetUserId(), "Session.View"))
                            return RedirectToAction("Details", new { id = result.RoomSessionId });
                        return RedirectToAction("WalkIn");
                    }
                    ModelState.AddModelError("", result.Error);
                }
                SetRoomOptions(db);
                return View(model);
            }
        }

        [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("Session.Extend")]
        public ActionResult Extend(int id, int minutes)
        {
            using (var db = new ApplicationDbContext())
            {
                var result = new RoomSessionService(db, new SystemClock())
                    .ExtendByStaff(id, minutes, User.Identity.GetUserId());
                TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
                    ? "Đã gia hạn thêm " + minutes + " phút." : result.Error;
            }
            return RedirectToAction("Details", new { id });
        }

        private void SetRoomOptions(ApplicationDbContext db)
        {
            ViewBag.Rooms = new SelectList(db.Rooms.AsNoTracking().Where(room => room.IsActive)
                .OrderBy(room => room.RoomCode).ToList(), "RoomId", "Name");
        }
    }
}
