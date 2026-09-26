using System;
using System.Web.Mvc;
using MusicBoxManagement.Models;
using MusicBoxManagement.Services;

namespace MusicBoxManagement.Controllers
{
    public class PublicRoomsController : Controller
    {
        public ActionResult Index()
        {
            using (var db = new ApplicationDbContext())
                return View(new PublicRoomCatalogService(db).List());
        }

        public ActionResult Details(int id)
        {
            using (var db = new ApplicationDbContext())
            {
                var room = new PublicRoomCatalogService(db).Get(id);
                if (room == null) return HttpNotFound();
                var today = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-dd");
                return View(new PublicRoomDetailsViewModel
                {
                    Room = room,
                    Booking = new GuestBookingFormViewModel { RoomId = id, BookingDate = today }
                });
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Book([Bind(Prefix = "Booking")] GuestBookingFormViewModel booking)
        {
            if (booking == null) return new HttpStatusCodeResult(400);
            using (var db = new ApplicationDbContext())
            {
                var room = new PublicRoomCatalogService(db).Get(booking.RoomId);
                if (room == null) return HttpNotFound();

                if (ModelState.IsValid)
                {
                    var date = GuestBookingDateParser.Parse(booking.BookingDate, booking.StartSlot);
                    if (!date.IsValid)
                        ModelState.AddModelError("Booking.BookingDate", "Ngày hoặc giờ bắt đầu không hợp lệ.");
                    else
                    {
                        var result = new ReservationService(db, new SystemClock())
                            .CreateGuest(booking.RoomId, booking.FullName, booking.PhoneNumber,
                                date.StartTimeUtc, booking.DurationMinutes.Value);
                        if (result.Succeeded)
                        {
                            TempData["BookingSummary"] = room.Name + " · " + date.StartTimeUtc.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy HH:mm");
                            return RedirectToAction("BookingSuccess");
                        }
                        ModelState.AddModelError("", result.Error);
                    }
                }

                return View("Details", new PublicRoomDetailsViewModel { Room = room, Booking = booking });
            }
        }

        public ActionResult BookingSuccess()
        {
            var summary = TempData["BookingSummary"] as string;
            if (summary == null) return RedirectToAction("Index");
            ViewBag.BookingSummary = summary;
            return View();
        }
    }
}
