using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using MusicBoxManagement.Models;

namespace MusicBoxManagement.Services
{
    public sealed class BillingPreview
    {
        public DateTimeOffset UpdatedAt { get; set; }
        public DateTimeOffset ActualStartTime { get; set; }
        public decimal HourlyRate { get; set; }
        public decimal UsedMinutes { get; set; }
        public decimal RoomCharge { get; set; }
        public decimal ServiceCharge { get; set; }
        public decimal TotalAmount { get { return RoomCharge + ServiceCharge; } }
    }

    public sealed class BillingService
    {
        private readonly ApplicationDbContext db;
        private readonly IClock clock;

        public BillingService(ApplicationDbContext db, IClock clock)
        {
            this.db = db;
            this.clock = clock;
        }

        public BillingPreview GetPreview(int sessionId)
        {
            var session = db.RoomSessions.AsNoTracking()
                .SingleOrDefault(item => item.RoomSessionId == sessionId &&
                    item.Status == RoomSessionStatuses.Active);
            if (session == null) return null;

            var completedItems = db.OrderItems.AsNoTracking()
                .Where(item => item.Order.RoomSessionId == sessionId &&
                    item.Order.Status == OrderStatuses.Completed)
                .Select(item => new { item.Quantity, item.UnitPrice })
                .ToList();
            var serviceCharge = completedItems.Sum(item => item.Quantity * item.UnitPrice);
            return Calculate(session.ActualStartTime, session.HourlyRate, serviceCharge, clock.UtcNow);
        }

        public static BillingPreview Calculate(DateTimeOffset startTime, decimal hourlyRate,
            decimal serviceCharge, DateTimeOffset endTime)
        {
            if (endTime < startTime || hourlyRate <= 0 || serviceCharge < 0)
                throw new ArgumentOutOfRangeException("endTime", "Thời gian hoặc đơn giá không hợp lệ.");
            var usedMinutes = (decimal)(endTime - startTime).Ticks / TimeSpan.TicksPerMinute;
            var roomCharge = Math.Round(hourlyRate * usedMinutes / 60m, 0, MidpointRounding.AwayFromZero);
            return new BillingPreview
            {
                UpdatedAt = endTime,
                ActualStartTime = startTime,
                HourlyRate = hourlyRate,
                UsedMinutes = usedMinutes,
                RoomCharge = roomCharge,
                ServiceCharge = serviceCharge
            };
        }
    }
}
