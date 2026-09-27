using System;
using System.Collections.Generic;
using System.Linq;
using MusicBoxManagement.Models;

namespace MusicBoxManagement.Services
{
    public sealed class WalkInCheckResult
    {
        public bool IsValid { get; private set; }
        public string Error { get; private set; }
        public DateTimeOffset WarningEndUtc { get; private set; }

        public static WalkInCheckResult Success(DateTimeOffset warningEndUtc)
        {
            return new WalkInCheckResult { IsValid = true, WarningEndUtc = warningEndUtc };
        }

        public static WalkInCheckResult Failure(string error)
        {
            return new WalkInCheckResult { Error = error };
        }
    }

    public static class WalkInRules
    {
        public static WalkInCheckResult Validate(int roomId, int customerId, DateTimeOffset nowUtc,
            bool roomActive, IEnumerable<Reservation> reservations, IEnumerable<RoomSession> sessions)
        {
            var localTime = nowUtc.ToOffset(TimeSpan.FromHours(7)).TimeOfDay;
            var open = (localTime >= TimeSpan.FromHours(9) && localTime < TimeSpan.FromHours(12)) ||
                (localTime >= TimeSpan.FromHours(13) && localTime < TimeSpan.FromHours(23));
            if (!open) return WalkInCheckResult.Failure("Hiện ngoài giờ nhận phòng.");
            if (!roomActive) return WalkInCheckResult.Failure("Phòng đang ngừng hoạt động.");
            if (sessions.Any(item => item.Status == RoomSessionStatuses.Active &&
                (item.RoomId == roomId || item.CustomerId == customerId)))
                return WalkInCheckResult.Failure("Phòng hoặc khách đang có phiên sử dụng.");
            if (reservations.Any(item => item.Status == ReservationStatuses.Confirmed &&
                (item.RoomId == roomId || item.CustomerId == customerId) &&
                item.StartTime <= nowUtc && nowUtc < item.StartTime.AddMinutes(15)))
                return WalkInCheckResult.Failure("Phòng hoặc khách đang có đặt phòng tới lượt. Vui lòng xử lý đặt phòng trước.");

            return WalkInCheckResult.Success(GetWarningEnd(roomId, customerId, nowUtc, nowUtc, reservations));
        }

        public static DateTimeOffset GetWarningEnd(int roomId, int customerId,
            DateTimeOffset actualStartUtc, DateTimeOffset nowUtc, IEnumerable<Reservation> reservations)
        {
            var offset = TimeSpan.FromHours(7);
            var localStart = actualStartUtc.ToOffset(offset);
            var shiftEndHour = localStart.TimeOfDay < TimeSpan.FromHours(12) ? 12 : 23;
            var shiftEnd = new DateTimeOffset(localStart.Date.AddHours(shiftEndHour), offset).ToUniversalTime();
            var nextBooking = reservations
                .Where(item => item.Status == ReservationStatuses.Confirmed &&
                    (item.RoomId == roomId || item.CustomerId == customerId) &&
                    item.StartTime > actualStartUtc && nowUtc < item.StartTime.AddMinutes(15))
                .OrderBy(item => item.StartTime)
                .FirstOrDefault();
            return nextBooking != null && nextBooking.StartTime < shiftEnd ? nextBooking.StartTime : shiftEnd;
        }
    }
}
