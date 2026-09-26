using System;
using System.Collections.Generic;
using System.Linq;
using MusicBoxManagement.Models;

namespace MusicBoxManagement.Services
{
    public static class AvailabilityRules
    {
        public static string FindConflict(int roomId, int? customerId, DateTimeOffset startUtc,
            DateTimeOffset endUtc, DateTimeOffset nowUtc,
            IEnumerable<Reservation> reservations, IEnumerable<RoomSession> sessions)
        {
            var bookingList = reservations.ToList();
            var sessionList = sessions.ToList();

            if (startUtc == nowUtc && sessionList.Any(session => session.Status == RoomSessionStatuses.Active && session.RoomId == roomId))
                return "Phòng đang có khách sử dụng.";

            if (IsRoomHeld(roomId, startUtc, endUtc, nowUtc, bookingList, sessionList))
                return "Phòng đã có lịch trong khoảng giờ này.";

            if (!customerId.HasValue) return null;

            if (startUtc == nowUtc && sessionList.Any(session => session.Status == RoomSessionStatuses.Active && session.CustomerId == customerId.Value))
                return "Khách đang sử dụng phòng khác.";

            if (HasHeldInterval(bookingList, sessionList, item => item.CustomerId == customerId.Value,
                item => item.CustomerId == customerId.Value, startUtc, endUtc, nowUtc))
                return "Khách đã có lịch trong khoảng giờ này.";

            return null;
        }

        public static bool IsRoomHeld(int roomId, DateTimeOffset startUtc, DateTimeOffset endUtc,
            DateTimeOffset nowUtc, IEnumerable<Reservation> reservations, IEnumerable<RoomSession> sessions)
        {
            return HasHeldInterval(reservations, sessions, item => item.RoomId == roomId,
                item => item.RoomId == roomId, startUtc, endUtc, nowUtc);
        }

        private static bool HasHeldInterval(IEnumerable<Reservation> reservations, IEnumerable<RoomSession> sessions,
            Func<Reservation, bool> reservationMatches, Func<RoomSession, bool> sessionMatches,
            DateTimeOffset startUtc, DateTimeOffset endUtc, DateTimeOffset nowUtc)
        {
            if (reservations.Any(item => reservationMatches(item)
                && item.Status == ReservationStatuses.Confirmed
                && nowUtc < item.StartTime.AddMinutes(15)
                && Overlaps(startUtc, endUtc, item.StartTime, item.EndTime)))
                return true;

            return sessions.Any(item => sessionMatches(item)
                && item.Status == RoomSessionStatuses.Active
                && item.ReservationId.HasValue
                && item.ExpectedEndTime.HasValue
                && Overlaps(startUtc, endUtc, item.ActualStartTime, item.ExpectedEndTime.Value));
        }

        public static bool Overlaps(DateTimeOffset firstStart, DateTimeOffset firstEnd,
            DateTimeOffset secondStart, DateTimeOffset secondEnd)
        {
            return firstStart < secondEnd && firstEnd > secondStart;
        }
    }
}
