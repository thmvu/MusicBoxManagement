using System;
using System.Globalization;
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

        public ActionResult Details(int id, string date)
        {
            DateTime localDate;
            if (!TryGetScheduleDate(date, out localDate)) return new HttpStatusCodeResult(400);
            using (var db = new ApplicationDbContext())
            {
                var room = new PublicRoomCatalogService(db).Get(id);
                if (room == null) return HttpNotFound();
                var booking = new GuestBookingFormViewModel { RoomId = id, BookingDate = localDate.ToString("yyyy-MM-dd") };
                return View(BuildDetails(db, room, booking, localDate));
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

                DateTime localDate;
                if (!TryGetScheduleDate(booking.BookingDate, out localDate))
                    localDate = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).Date;
                return View("Details", BuildDetails(db, room, booking, localDate));
            }
        }

        public ActionResult BookingSuccess()
        {
            var summary = TempData["BookingSummary"] as string;
            if (summary == null) return RedirectToAction("Index");
            ViewBag.BookingSummary = summary;
            return View();
        }

        private static PublicRoomDetailsViewModel BuildDetails(ApplicationDbContext db,
            PublicRoomViewModel room, GuestBookingFormViewModel booking, DateTime localDate)
        {
            return new PublicRoomDetailsViewModel
            {
                Room = room,
                Booking = booking,
                ScheduleDate = localDate.ToString("yyyy-MM-dd"),
                ScheduleSlots = new GuestRoomScheduleService(db, new SystemClock()).GetDay(room.RoomId, localDate)
            };
        }

        private static bool TryGetScheduleDate(string input, out DateTime localDate)
        {
            var today = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).Date;
            if (string.IsNullOrEmpty(input))
            {
                localDate = today;
                return true;
            }
            return DateTime.TryParseExact(input, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out localDate)
                && localDate >= today && localDate <= today.AddDays(30);
        }
    }
}
