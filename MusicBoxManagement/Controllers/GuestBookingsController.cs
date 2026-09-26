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
                    return View("Index", BuildModel(phoneNumber, service));
                }
            }
            TempData["Success"] = "Đã hủy đặt phòng.";
            TempData["LookupPhone"] = phoneNumber;
            return RedirectToAction("Index");
        }

        private static GuestLookupViewModel BuildModel(string phoneNumber)
        {
            using (var db = new ApplicationDbContext())
                return BuildModel(phoneNumber, new ReservationService(db, new SystemClock()));
        }

        private static GuestLookupViewModel BuildModel(string phoneNumber, ReservationService service)
        {
            var result = service.LookupGuest(phoneNumber);
            return new GuestLookupViewModel
            {
                PhoneNumber = phoneNumber,
                HasSearched = result.IsValid,
                Bookings = result.Bookings
            };
        }
    }
}
