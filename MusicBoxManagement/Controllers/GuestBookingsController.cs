using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using MusicBoxManagement.Models;
using MusicBoxManagement.Services;

namespace MusicBoxManagement.Controllers
{
    public class GuestBookingsController : Controller
    {
        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "*")]
        public ActionResult Index()
        {
            var phone = TempData["LookupPhone"] as string;
            if (phone == null) return View(new GuestLookupViewModel());
            return View(BuildModel(phone));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Lookup(GuestLookupViewModel model)
        {
            if (model == null) return new HttpStatusCodeResult(400);
            using (var db = new ApplicationDbContext())
            {
                new NoShowService(db, new SystemClock()).ProcessExpired();
                var result = new ReservationService(db, new SystemClock()).LookupGuest(model.PhoneNumber);
                if (!result.IsValid)
                    ModelState.AddModelError("PhoneNumber", "Số điện thoại không hợp lệ.");
                model.HasSearched = result.IsValid;
                model.Bookings = result.Bookings;
                model.ActiveSessions = result.ActiveSessions;
                if (result.IsValid) PopulateOrders(model, db);
            }
            return View("Index", model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Cancel(int reservationId, string phoneNumber)
        {
            using (var db = new ApplicationDbContext())
            {
                var service = new ReservationService(db, new SystemClock());
                var result = service.CancelGuest(reservationId, phoneNumber);
                if (!result.Succeeded)
                {
                    ModelState.AddModelError("", result.Error);
                    return View("Index", BuildModel(phoneNumber, service, db));
                }
            }
            TempData["Success"] = "Đã hủy đặt phòng.";
            TempData["LookupPhone"] = phoneNumber;
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Extend(int sessionId, int minutes, string phoneNumber)
        {
            using (var db = new ApplicationDbContext())
            {
                var service = new RoomSessionService(db, new SystemClock());
                var result = service.ExtendGuest(sessionId, minutes, phoneNumber);
                if (!result.Succeeded)
                {
                    ModelState.AddModelError("", result.Error);
                    return View("Index", BuildModel(phoneNumber));
                }
            }
            TempData["Success"] = "Đã gia hạn phiên thêm " + minutes + " phút.";
            TempData["LookupPhone"] = phoneNumber;
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult CreateOrder(int sessionId, string phoneNumber, IList<OrderLineInput> lines)
        {
            var selected = (lines ?? new List<OrderLineInput>()).Where(line => line.Quantity != 0).ToList();
            using (var db = new ApplicationDbContext())
            {
                var result = new OrderService(db, new SystemClock()).CreateGuest(sessionId, phoneNumber, selected);
                TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
                    ? "Đã gửi yêu cầu món #" + result.OrderId + ". Nhân viên sẽ xác nhận sau khi phục vụ."
                    : result.Error;
            }
            TempData["LookupPhone"] = phoneNumber;
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult CancelOrder(int orderId, string phoneNumber)
        {
            using (var db = new ApplicationDbContext())
            {
                var result = new OrderService(db, new SystemClock()).CancelGuest(orderId, phoneNumber);
                TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
                    ? "Đã hủy đơn món #" + orderId + "." : result.Error;
            }
            TempData["LookupPhone"] = phoneNumber;
            return RedirectToAction("Index");
        }

        private static GuestLookupViewModel BuildModel(string phoneNumber)
        {
            using (var db = new ApplicationDbContext())
                return BuildModel(phoneNumber, new ReservationService(db, new SystemClock()), db);
        }

        private static GuestLookupViewModel BuildModel(string phoneNumber, ReservationService service,
            ApplicationDbContext db = null)
        {
            var result = service.LookupGuest(phoneNumber);
            var model = new GuestLookupViewModel
            {
                PhoneNumber = phoneNumber,
                HasSearched = result.IsValid,
                Bookings = result.Bookings,
                ActiveSessions = result.ActiveSessions
            };
            if (result.IsValid && db != null) PopulateOrders(model, db);
            return model;
        }

        private static void PopulateOrders(GuestLookupViewModel model, ApplicationDbContext db)
        {
            if (!model.ActiveSessions.Any()) return;
            model.Menu = db.Services.AsNoTracking().Where(item => item.IsActive)
                .OrderBy(item => item.Category).ThenBy(item => item.Name)
                .Select(item => new OrderLineChoiceViewModel
                { ServiceId = item.ServiceId, Name = item.Name, Category = item.Category, Price = item.Price })
                .ToList();
            var orders = new OrderReadService(db);
            var billing = new BillingService(db, new SystemClock());
            foreach (var session in model.ActiveSessions)
            {
                session.Orders = orders.ForSession(session.RoomSessionId);
                session.Billing = billing.GetPreview(session.RoomSessionId);
            }
        }
    }
}
