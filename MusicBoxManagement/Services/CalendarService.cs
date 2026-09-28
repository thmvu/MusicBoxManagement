using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using MusicBoxManagement.Models;

namespace MusicBoxManagement.Services
{
    public sealed class CalendarService
    {
        private readonly ApplicationDbContext db;
        private readonly IClock clock;

        public CalendarService(ApplicationDbContext db, IClock clock)
        {
            this.db = db;
            this.clock = clock;
        }

        public IList<CalendarEventViewModel> GetEvents(DateTimeOffset rangeStart, DateTimeOffset rangeEnd,
            int? roomId, Func<int, string> reservationUrl, Func<int, string> sessionUrl)
        {
            var now = clock.UtcNow;
            var reservations = db.Reservations.AsNoTracking()
                .Where(item => item.Status == ReservationStatuses.Confirmed &&
                    item.StartTime < rangeEnd && item.EndTime > rangeStart &&
                    (!roomId.HasValue || item.RoomId == roomId.Value))
                .Select(item => new { item.ReservationId, item.StartTime, item.EndTime,
                    RoomCode = item.Room.RoomCode, CustomerName = item.Customer.FullName }).ToList()
                .Where(item => now < item.StartTime.AddMinutes(15)).ToList();
            var sessions = db.RoomSessions.AsNoTracking()
                .Where(item => item.ActualStartTime < rangeEnd &&
                    (item.Status == RoomSessionStatuses.Active ||
                     (item.Status == RoomSessionStatuses.Completed && item.ActualEndTime > rangeStart)) &&
                    (!roomId.HasValue || item.RoomId == roomId.Value))
                .Select(item => new { item.RoomSessionId, item.ReservationId, item.Status,
                    item.RoomId, item.CustomerId, item.ActualStartTime, item.ActualEndTime, item.ExpectedEndTime,
                    item.RoomCodeSnapshot, CustomerName = item.Customer.FullName }).ToList();

            var walkIns = sessions.Where(item => item.Status == RoomSessionStatuses.Active &&
                !item.ReservationId.HasValue).ToList();
            var warningReservations = new List<Reservation>();
            if (walkIns.Count > 0)
            {
                var roomIds = walkIns.Select(item => item.RoomId).Distinct().ToList();
                var customerIds = walkIns.Select(item => item.CustomerId).Distinct().ToList();
                var earliestStart = walkIns.Min(item => item.ActualStartTime);
                warningReservations = db.Reservations.AsNoTracking()
                    .Where(item => item.Status == ReservationStatuses.Confirmed &&
                        item.StartTime > earliestStart &&
                        (roomIds.Contains(item.RoomId) || customerIds.Contains(item.CustomerId)))
                    .ToList();
            }

            var events = new List<CalendarEventViewModel>();
            foreach (var item in reservations)
                events.Add(new CalendarEventViewModel
                {
                    Title = item.RoomCode + " · Đã đặt · " + item.CustomerName,
                    Start = LocalTime(item.StartTime), End = LocalTime(item.EndTime),
                    Url = reservationUrl(item.ReservationId), ClassName = "calendar-reserved"
                });
            foreach (var item in sessions)
            {
                var active = item.Status == RoomSessionStatuses.Active;
                var end = active
                    ? (item.ReservationId.HasValue && item.ExpectedEndTime > now ? item.ExpectedEndTime.Value : now)
                    : item.ActualEndTime.Value;
                if (end <= rangeStart || end <= item.ActualStartTime) continue;
                var label = active
                    ? (item.ReservationId.HasValue ? "Đang sử dụng" : "Walk-in · chưa chốt giờ trả")
                    : "Đã hoàn tất";
                if (active && !item.ReservationId.HasValue)
                {
                    var warningEnd = WalkInRules.GetWarningEnd(item.RoomId, item.CustomerId,
                        item.ActualStartTime, now, warningReservations);
                    label += " · cần trả trước " + warningEnd.ToOffset(TimeSpan.FromHours(7)).ToString("HH:mm");
                }
                if (active && item.ExpectedEndTime.HasValue && item.ExpectedEndTime < now)
                    label += " · quá giờ";
                events.Add(new CalendarEventViewModel
                {
                    Title = item.RoomCodeSnapshot + " · " + label + " · " + item.CustomerName,
                    Start = LocalTime(item.ActualStartTime), End = LocalTime(end),
                    Url = sessionUrl(item.RoomSessionId),
                    ClassName = active ? "calendar-active" : "calendar-completed"
                });
            }
            return events;
        }

        private static string LocalTime(DateTimeOffset time)
        {
            return time.ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-ddTHH:mm:ss");
        }
    }
}
