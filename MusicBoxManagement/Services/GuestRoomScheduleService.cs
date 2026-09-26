using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using MusicBoxManagement.Models;

namespace MusicBoxManagement.Services
{
    public sealed class GuestRoomScheduleService
    {
        private readonly ApplicationDbContext db;
        private readonly IClock clock;

        public GuestRoomScheduleService(ApplicationDbContext db, IClock clock)
        {
            this.db = db;
            this.clock = clock;
        }

        public IList<GuestScheduleSlot> GetDay(int roomId, DateTime localDate)
        {
            if (!db.Rooms.AsNoTracking().Any(room => room.RoomId == roomId && room.IsActive))
                return null;

            var offset = TimeSpan.FromHours(7);
            var rangeStart = new DateTimeOffset(localDate.Date, offset).ToUniversalTime();
            var rangeEnd = rangeStart.AddDays(1);
            var reservations = db.Reservations.AsNoTracking()
                .Where(item => item.RoomId == roomId && item.Status == ReservationStatuses.Confirmed
                    && item.StartTime < rangeEnd && item.EndTime > rangeStart)
                .ToList();
            var sessions = db.RoomSessions.AsNoTracking()
                .Where(item => item.RoomId == roomId && item.ActualStartTime < rangeEnd
                    && (item.Status == RoomSessionStatuses.Active
                        || (item.Status == RoomSessionStatuses.Completed && item.ActualEndTime > rangeStart)))
                .ToList();

            return GuestRoomScheduleRules.Build(roomId, localDate, clock.UtcNow, reservations, sessions);
        }
    }
}
