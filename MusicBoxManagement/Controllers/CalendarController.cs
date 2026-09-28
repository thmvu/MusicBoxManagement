using System;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using MusicBoxManagement.Authorization;
using MusicBoxManagement.Models;
using MusicBoxManagement.Services;

namespace MusicBoxManagement.Controllers
{
    public class CalendarController : Controller
    {
        [PermissionAuthorize("Calendar.View")]
        public ActionResult Index(int? roomId)
        {
            using (var db = new ApplicationDbContext())
            {
                var rooms = db.Rooms.AsNoTracking().OrderBy(item => item.RoomCode)
                    .Select(item => new CalendarRoomViewModel
                    { RoomId = item.RoomId, RoomCode = item.RoomCode }).ToList();
                if (roomId.HasValue && !rooms.Any(item => item.RoomId == roomId.Value))
                    return HttpNotFound();
                return View(new CalendarViewModel { RoomId = roomId, Rooms = rooms });
            }
        }

        [PermissionAuthorize("Calendar.View")]
        public ActionResult Events(string start, string end, int? roomId)
        {
            DateTime startDate, endDate;
            if (string.IsNullOrEmpty(start) || start.Length < 10 ||
                string.IsNullOrEmpty(end) || end.Length < 10 ||
                !DateTime.TryParseExact(start.Substring(0, 10), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out startDate) ||
                !DateTime.TryParseExact(end.Substring(0, 10), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out endDate))
                return new HttpStatusCodeResult(400);
            var offset = TimeSpan.FromHours(7);
            var rangeStart = new DateTimeOffset(startDate, offset).ToUniversalTime();
            var rangeEnd = new DateTimeOffset(endDate, offset).ToUniversalTime();
            if (
                rangeEnd <= rangeStart || rangeEnd - rangeStart > TimeSpan.FromDays(8))
                return new HttpStatusCodeResult(400);
            using (var db = new ApplicationDbContext())
            {
                var items = new CalendarService(db, new SystemClock()).GetEvents(
                    rangeStart, rangeEnd, roomId,
                    id => Url.Action("Details", "Reservations", new { id }),
                    id => Url.Action("Details", "RoomSessions", new { id }));
                return Json(items.Select(item => new
                {
                    title = item.Title, start = item.Start, end = item.End,
                    url = item.Url, className = item.ClassName
                }), JsonRequestBehavior.AllowGet);
            }
        }
    }
}
