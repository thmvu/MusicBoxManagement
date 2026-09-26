using System.Data;
using System.Linq;
using MusicBoxManagement.Models;

namespace MusicBoxManagement.Services
{
    public sealed class NoShowService
    {
        private readonly ApplicationDbContext db;
        private readonly IClock clock;

        public NoShowService(ApplicationDbContext db, IClock clock)
        {
            this.db = db;
            this.clock = clock;
        }

        public int ProcessExpired()
        {
            using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                var now = clock.UtcNow;
                var cutoff = now.AddMinutes(-15);
                var expired = db.Reservations
                    .Where(item => item.Status == ReservationStatuses.Confirmed && item.StartTime <= cutoff)
                    .ToList();

                foreach (var reservation in expired)
                {
                    reservation.Status = ReservationStatuses.NoShow;
                    db.AuditLogs.Add(new AuditLog
                    {
                        ActorType = "System",
                        Action = "NoShow",
                        EntityName = "Reservation",
                        EntityId = reservation.ReservationId.ToString(),
                        Description = "Quá 15 phút chưa nhận phòng.",
                        CreatedAt = now
                    });
                }

                if (expired.Count > 0) db.SaveChanges();
                transaction.Commit();
                return expired.Count;
            }
        }
    }
}
