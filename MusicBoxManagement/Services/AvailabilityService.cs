using System;
using System.Data.Entity;
using System.Linq;
using MusicBoxManagement.Models;

namespace MusicBoxManagement.Services
{
    public sealed class AvailabilityCheckResult
    {
        public bool IsAvailable { get; private set; }
        public string Error { get; private set; }
        public DateTimeOffset EndTimeUtc { get; private set; }

        public static AvailabilityCheckResult Available(DateTimeOffset endTimeUtc)
        {
            return new AvailabilityCheckResult { IsAvailable = true, EndTimeUtc = endTimeUtc };
        }

        public static AvailabilityCheckResult Unavailable(string error)
        {
            return new AvailabilityCheckResult { Error = error };
        }
    }

    public sealed class AvailabilityService
    {
        private readonly ApplicationDbContext db;
        private readonly IClock clock;

        public AvailabilityService(ApplicationDbContext db, IClock clock)
        {
            this.db = db;
            this.clock = clock;
        }

        // customerId is null when a Guest views a room before entering a phone number.
        public AvailabilityCheckResult CheckReservation(int roomId, int? customerId, DateTimeOffset startUtc, int durationMinutes)
        {
            var initialTimeResult = BookingTimeRules.Validate(startUtc, durationMinutes, clock.UtcNow);
            if (!initialTimeResult.IsValid) return AvailabilityCheckResult.Unavailable(initialTimeResult.Error);

            if (!db.Rooms.AsNoTracking().Any(room => room.RoomId == roomId && room.IsActive))
                return AvailabilityCheckResult.Unavailable("Phòng không tồn tại hoặc đang ngừng hoạt động.");

            if (customerId.HasValue && !db.Customers.AsNoTracking().Any(customer => customer.CustomerId == customerId.Value))
                return AvailabilityCheckResult.Unavailable("Khách hàng không tồn tại.");

            // Narrow both queries in SQL; only candidate intervals are evaluated in memory.
            var endUtc = initialTimeResult.EndTimeUtc;
            var checkCustomer = customerId.HasValue;
            var selectedCustomerId = customerId.GetValueOrDefault();
            var reservations = db.Reservations.AsNoTracking()
                .Where(item => item.Status == ReservationStatuses.Confirmed
                    && (item.RoomId == roomId || (checkCustomer && item.CustomerId == selectedCustomerId))
                    && item.StartTime < endUtc && item.EndTime > startUtc)
                .ToList();
            var sessions = db.RoomSessions.AsNoTracking()
                .Where(item => item.Status == RoomSessionStatuses.Active
                    && (item.RoomId == roomId || (checkCustomer && item.CustomerId == selectedCustomerId)))
                .ToList();

            // Read time after database calls so the validation uses a fresh value inside a future write transaction.
            var nowUtc = clock.UtcNow;
            var timeResult = BookingTimeRules.Validate(startUtc, durationMinutes, nowUtc);
            if (!timeResult.IsValid) return AvailabilityCheckResult.Unavailable(timeResult.Error);

            var conflict = AvailabilityRules.FindConflict(roomId, customerId, startUtc, timeResult.EndTimeUtc,
                nowUtc, reservations, sessions);
            return conflict == null
                ? AvailabilityCheckResult.Available(timeResult.EndTimeUtc)
                : AvailabilityCheckResult.Unavailable(conflict);
        }
    }
}
