using System;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using MusicBoxManagement.Authorization;
using MusicBoxManagement.Models;
using MusicBoxManagement.Services;

namespace MusicBoxManagement.Controllers
{
    public class ReservationsController : Controller
    {
        [PermissionAuthorize("Reservation.View")]
        public ActionResult Index(string date)
        {
            DateTime localDate;
            if (string.IsNullOrWhiteSpace(date))
                localDate = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).Date;
            else if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out localDate))
                return new HttpStatusCodeResult(400);

            var offset = TimeSpan.FromHours(7);
            var start = new DateTimeOffset(localDate, offset).ToUniversalTime();
            var end = start.AddDays(1);
            using (var db = new ApplicationDbContext())
            {
                new NoShowService(db, new SystemClock()).ProcessExpired();
                var items = db.Reservations.AsNoTracking()
                    .Where(item => item.StartTime >= start && item.StartTime < end)
                    .OrderBy(item => item.StartTime)
                    .Select(item => new ReservationListItemViewModel
                    {
                        ReservationId = item.ReservationId,
                        RoomName = item.Room.Name,
                        CustomerName = item.Customer.FullName,
                        PhoneNumber = item.Customer.PhoneNumber,
                        StartTime = item.StartTime,
                        EndTime = item.EndTime,
                        Status = item.Status
                    }).ToList();
                ViewBag.CanCreate = new PermissionService(db).HasPermission(User.Identity.GetUserId(), "Reservation.Create");
                return View(new ReservationListViewModel { Date = localDate.ToString("yyyy-MM-dd"), Items = items });
            }
        }

        [PermissionAuthorize("Reservation.View")]
        public ActionResult Details(int id)
        {
            using (var db = new ApplicationDbContext())
            {
                new NoShowService(db, new SystemClock()).ProcessExpired();
                var item = db.Reservations.AsNoTracking()
                    .Where(reservation => reservation.ReservationId == id)
                    .Select(reservation => new ReservationDetailsViewModel
                    {
                        ReservationId = reservation.ReservationId,
                        RoomName = reservation.Room.Name,
                        CustomerName = reservation.Customer.FullName,
                        PhoneNumber = reservation.Customer.PhoneNumber,
                        StartTime = reservation.StartTime,
                        EndTime = reservation.EndTime,
                        Status = reservation.Status,
                        CancellationReason = reservation.CancellationReason
                    }).SingleOrDefault();
                if (item == null) return HttpNotFound();
                item.CanCancel = item.Status == ReservationStatuses.Confirmed &&
                    DateTimeOffset.UtcNow < item.StartTime.AddMinutes(15) &&
                    new PermissionService(db).HasPermission(User.Identity.GetUserId(), "Reservation.Cancel");
                return View(item);
            }
        }

        [PermissionAuthorize("Reservation.Create")]
        public ActionResult Create()
        {
            using (var db = new ApplicationDbContext()) SetRoomOptions(db);
            return View(new GuestBookingFormViewModel
            {
                BookingDate = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-dd")
            });
        }

        [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("Reservation.Create")]
        public ActionResult Create(GuestBookingFormViewModel model)
        {
            if (model == null) return new HttpStatusCodeResult(400);
            using (var db = new ApplicationDbContext())
            {
                if (ModelState.IsValid)
                {
                    var date = GuestBookingDateParser.Parse(model.BookingDate, model.StartSlot);
                    if (!date.IsValid)
                        ModelState.AddModelError("BookingDate", "Ngày hoặc giờ bắt đầu không hợp lệ.");
                    else
                    {
                        var result = new ReservationService(db, new SystemClock())
                            .CreateStaff(model.RoomId, model.FullName, model.PhoneNumber,
                                date.StartTimeUtc, model.DurationMinutes.Value, User.Identity.GetUserId());
                        if (result.Succeeded)
                        {
                            TempData["Success"] = "Đã tạo đặt phòng.";
                            if (new PermissionService(db).HasPermission(User.Identity.GetUserId(), "Reservation.View"))
                                return RedirectToAction("Details", new { id = result.ReservationId });
                            return RedirectToAction("Create");
                        }
                        ModelState.AddModelError("", result.Error);
                    }
                }
                SetRoomOptions(db);
                return View(model);
            }
        }

        [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("Reservation.Cancel")]
        public ActionResult Cancel(int id, string reason)
        {
            using (var db = new ApplicationDbContext())
            {
                var result = new ReservationService(db, new SystemClock())
                    .CancelByStaff(id, reason, User.Identity.GetUserId());
                TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
                    ? "Đã hủy đặt phòng." : result.Error;
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
