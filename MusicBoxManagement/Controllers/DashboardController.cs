using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using MusicBoxManagement.Authorization;
using MusicBoxManagement.Models;
using MusicBoxManagement.Services;

namespace MusicBoxManagement.Controllers
{
    public class DashboardController : Controller
    {
        [PermissionAuthorize("Dashboard.View")]
        public ActionResult Index()
        {
            var now = DateTimeOffset.UtcNow;
            var local = now.ToOffset(TimeSpan.FromHours(7));
            var todayStart = new DateTimeOffset(local.Date, TimeSpan.FromHours(7)).ToUniversalTime();
            var tomorrowStart = todayStart.AddDays(1);
            var noShowWindowStart = now.AddMinutes(-15);
            var noShowWindowEnd = now.AddMinutes(-10);

            using (var db = new ApplicationDbContext())
            {
                new NoShowService(db, new SystemClock()).ProcessExpired();
                var sessions = db.RoomSessions.AsNoTracking()
                    .Where(item => item.Status == RoomSessionStatuses.Active)
                    .OrderBy(item => item.ActualStartTime)
                    .Select(item => new DashboardSessionViewModel
                    {
                        RoomSessionId = item.RoomSessionId,
                        ReservationId = item.ReservationId,
                        RoomName = item.Room.Name,
                        CustomerName = item.Customer.FullName,
                        ActualStartTime = item.ActualStartTime,
                        ExpectedEndTime = item.ExpectedEndTime
                    }).ToList();
                var model = new DashboardViewModel
                {
                    UpdatedAt = now,
                    TotalRooms = db.Rooms.Count(room => room.IsActive),
                    OccupiedRooms = sessions.Count,
                    TodayReservations = db.Reservations.Count(item =>
                        item.StartTime >= todayStart && item.StartTime < tomorrowStart &&
                        item.Status != ReservationStatuses.Cancelled && item.Status != ReservationStatuses.NoShow),
                    ActiveSessions = sessions.Count,
                    PendingOrders = db.Orders.Count(item => item.Status == OrderStatuses.Pending),
                    Sessions = sessions
                };

                var upcoming = db.Reservations.AsNoTracking()
                    .Where(item => item.Status == ReservationStatuses.Confirmed &&
                        item.StartTime > noShowWindowStart &&
                        item.StartTime <= noShowWindowEnd)
                    .Select(item => new { item.ReservationId, item.Room.Name, item.StartTime })
                    .ToList();
                foreach (var item in upcoming)
                    model.Alerts.Add(new DashboardAlertViewModel
                    {
                        Title = "Đặt phòng sắp quá hạn nhận phòng",
                        Detail = item.Name + " · hạn " + item.StartTime.AddMinutes(15).ToOffset(TimeSpan.FromHours(7)).ToString("HH:mm"),
                        ActionText = "Xem đặt phòng",
                        ActionUrl = Url.Action("Details", "Reservations", new { id = item.ReservationId })
                    });
                foreach (var item in sessions.Where(item => item.ExpectedEndTime.HasValue && item.ExpectedEndTime < now))
                    model.Alerts.Add(new DashboardAlertViewModel
                    {
                        Title = "Phiên đã quá giờ dự kiến",
                        Detail = item.RoomName + " · " + item.CustomerName,
                        ActionText = "Xem phiên",
                        ActionUrl = Url.Action("Details", "RoomSessions", new { id = item.RoomSessionId })
                    });
                var sessionService = new RoomSessionService(db, new SystemClock());
                foreach (var item in sessions.Where(item => !item.ReservationId.HasValue))
                {
                    var warningEnd = sessionService.GetWalkInWarning(item.RoomSessionId);
                    if (!warningEnd.HasValue || warningEnd.Value > now.AddMinutes(15)) continue;
                    model.Alerts.Add(new DashboardAlertViewModel
                    {
                        Title = "Phiên trực tiếp sắp ảnh hưởng lượt kế tiếp",
                        Detail = item.RoomName + " · cần xem lại trước " + warningEnd.Value.ToOffset(TimeSpan.FromHours(7)).ToString("HH:mm"),
                        ActionText = "Xem phiên",
                        ActionUrl = Url.Action("Details", "RoomSessions", new { id = item.RoomSessionId })
                    });
                }
                if (model.PendingOrders > 0)
                    model.Alerts.Add(new DashboardAlertViewModel
                    {
                        Title = "Có đơn món đang chờ",
                        Detail = model.PendingOrders + " đơn chưa được xử lý."
                    });
                if ((local.Hour == 12 || local.Hour >= 23) && sessions.Count > 0)
                    model.Alerts.Add(new DashboardAlertViewModel
                    {
                        Title = "Còn khách ngoài giờ phục vụ",
                        Detail = sessions.Count + " phiên vẫn đang hoạt động."
                    });

                return View(model);
            }
        }
    }
}
