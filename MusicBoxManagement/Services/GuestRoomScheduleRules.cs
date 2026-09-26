using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MusicBoxManagement.Models;

namespace MusicBoxManagement.Services
{
    public static class GuestScheduleStatuses
    {
        public const string Available = "Available";
        public const string Busy = "Busy";
        public const string Past = "Past";
    }

    public static class GuestRoomScheduleRules
    {
        private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

        public static IList<GuestScheduleSlot> Build(int roomId, DateTime localDate, DateTimeOffset nowUtc,
            IEnumerable<Reservation> reservations, IEnumerable<RoomSession> sessions)
        {
            var bookingList = reservations.ToList();
            var sessionList = sessions.ToList();
            var slots = new List<GuestScheduleSlot>();
            for (var minute = 9 * 60; minute < 23 * 60; minute += 30)
            {
                if (minute >= 12 * 60 && minute < 13 * 60) continue;

                var localStart = new DateTimeOffset(localDate.Date.AddMinutes(minute), VietnamOffset);
                var localEnd = localStart.AddMinutes(30);
                var startUtc = localStart.ToUniversalTime();
                var endUtc = localEnd.ToUniversalTime();

                var held = AvailabilityRules.IsRoomHeld(roomId, startUtc, endUtc, nowUtc,
                    bookingList, sessionList);
                var used = sessionList.Any(session => IsActuallyUsed(session, startUtc, endUtc, nowUtc));
                var activeNow = sessionList.Any(session => session.Status == RoomSessionStatuses.Active
                    && session.ActualStartTime <= nowUtc && startUtc <= nowUtc && nowUtc < endUtc);
                var status = held || used || activeNow ? GuestScheduleStatuses.Busy
                    : startUtc < nowUtc ? GuestScheduleStatuses.Past : GuestScheduleStatuses.Available;

                slots.Add(new GuestScheduleSlot
                {
                    LocalStartLabel = localStart.ToString("HH:mm", CultureInfo.InvariantCulture),
                    LocalEndLabel = localEnd.ToString("HH:mm", CultureInfo.InvariantCulture),
                    StartTimeUtc = startUtc,
                    EndTimeUtc = endUtc,
                    Status = status
                });
            }
            return slots;
        }

        private static bool IsActuallyUsed(RoomSession session, DateTimeOffset slotStart,
            DateTimeOffset slotEnd, DateTimeOffset nowUtc)
        {
            if (session.Status != RoomSessionStatuses.Active && session.Status != RoomSessionStatuses.Completed)
                return false;
            var actualEnd = session.Status == RoomSessionStatuses.Active ? nowUtc : session.ActualEndTime;
            return actualEnd.HasValue && actualEnd.Value > session.ActualStartTime
                && AvailabilityRules.Overlaps(slotStart, slotEnd, session.ActualStartTime, actualEnd.Value);
        }
    }
}
