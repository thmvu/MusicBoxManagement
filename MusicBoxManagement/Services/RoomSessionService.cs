using System;
using System.Data;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using MusicBoxManagement.Models;

namespace MusicBoxManagement.Services
{
    public sealed class CheckInResult
    {
        public bool Succeeded { get; private set; }
        public int RoomSessionId { get; private set; }
        public string Error { get; private set; }

        public static CheckInResult Success(int sessionId)
        {
            return new CheckInResult { Succeeded = true, RoomSessionId = sessionId };
        }

        public static CheckInResult Failure(string error)
        {
            return new CheckInResult { Error = error };
        }
    }

    public sealed class RoomSessionService
    {
        private readonly ApplicationDbContext db;
        private readonly IClock clock;

        public RoomSessionService(ApplicationDbContext db, IClock clock)
        {
            this.db = db;
            this.clock = clock;
        }

        public CheckInResult CheckIn(int reservationId, string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return CheckInResult.Failure("Thiếu nhân viên thực hiện.");

            try
            {
                using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
                {
                    if (!db.Users.Any(user => user.Id == userId && user.IsActive))
                        return CheckInResult.Failure("Nhân viên không còn hoạt động.");

                    var existing = db.RoomSessions.AsNoTracking()
                        .SingleOrDefault(item => item.ReservationId == reservationId);
                    if (existing != null)
                        return CheckInResult.Success(existing.RoomSessionId);

                    var reservation = db.Reservations.Include("Room.RoomType")
                        .SingleOrDefault(item => item.ReservationId == reservationId);
                    if (reservation == null)
                        return CheckInResult.Failure("Không tìm thấy đặt phòng.");

                    var initialNow = clock.UtcNow;
                    var sessions = db.RoomSessions.AsNoTracking()
                        .Where(item => item.Status == RoomSessionStatuses.Active &&
                            (item.RoomId == reservation.RoomId || item.CustomerId == reservation.CustomerId))
                        .ToList();
                    var otherReservations = db.Reservations.AsNoTracking()
                        .Where(item => item.ReservationId != reservationId &&
                            item.Status == ReservationStatuses.Confirmed && item.EndTime > initialNow &&
                            (item.RoomId == reservation.RoomId || item.CustomerId == reservation.CustomerId))
                        .ToList();

                    var now = clock.UtcNow;
                    var check = CheckInRules.Validate(reservation, now, reservation.Room.IsActive,
                        otherReservations, sessions);
                    if (!check.IsValid) return CheckInResult.Failure(check.Error);

                    var session = new RoomSession
                    {
                        ReservationId = reservation.ReservationId,
                        RoomId = reservation.RoomId,
                        CustomerId = reservation.CustomerId,
                        ActualStartTime = now,
                        ExpectedEndTime = check.CandidateEndUtc,
                        Status = RoomSessionStatuses.Active,
                        HourlyRate = reservation.Room.RoomType.PricePerHour,
                        RoomCodeSnapshot = reservation.Room.RoomCode,
                        RoomTypeCodeSnapshot = reservation.Room.RoomType.Code,
                        RoomTypeNameSnapshot = reservation.Room.RoomType.Name
                    };
                    reservation.Status = ReservationStatuses.CheckedIn;
                    db.RoomSessions.Add(session);
                    db.SaveChanges();

                    db.AuditLogs.Add(new AuditLog
                    {
                        ActorType = "Staff",
                        UserId = userId,
                        Action = "CheckIn",
                        EntityName = "RoomSession",
                        EntityId = session.RoomSessionId.ToString(),
                        Description = "Nhận phòng từ đặt trước " + reservation.ReservationId,
                        CreatedAt = now
                    });
                    db.SaveChanges();
                    transaction.Commit();
                    return CheckInResult.Success(session.RoomSessionId);
                }
            }
            catch (Exception error)
            {
                if (!IsConcurrentChange(error)) throw;
                var existing = db.RoomSessions.AsNoTracking()
                    .SingleOrDefault(item => item.ReservationId == reservationId);
                return existing != null ? CheckInResult.Success(existing.RoomSessionId)
                    : CheckInResult.Failure("Dữ liệu vừa thay đổi. Vui lòng tải lại và thử lại.");
            }
        }

        private static bool IsConcurrentChange(Exception error)
        {
            for (var current = error; current != null; current = current.InnerException)
            {
                var sql = current as SqlException;
                if (sql != null && (sql.Number == 1205 || sql.Number == 2601 || sql.Number == 2627))
                    return true;
            }
            return false;
        }
    }
}
