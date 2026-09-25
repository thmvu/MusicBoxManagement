using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using MusicBoxManagement.Models;

namespace MusicBoxManagement.Services
{
    public sealed class ReservationCreateResult
    {
        public bool Succeeded { get; private set; }
        public int ReservationId { get; private set; }
        public string Error { get; private set; }

        public static ReservationCreateResult Success(int reservationId)
        {
            return new ReservationCreateResult { Succeeded = true, ReservationId = reservationId };
        }

        public static ReservationCreateResult Failure(string error)
        {
            return new ReservationCreateResult { Error = error };
        }
    }

    public sealed class ReservationService
    {
        private readonly ApplicationDbContext db;
        private readonly IClock clock;

        public ReservationService(ApplicationDbContext db, IClock clock)
        {
            this.db = db;
            this.clock = clock;
        }

        public ReservationCreateResult CreateGuest(int roomId, string fullName, string phoneNumber,
            DateTimeOffset startUtc, int durationMinutes)
        {
            var input = ReservationInputRules.Validate(fullName, phoneNumber);
            if (!input.IsValid) return ReservationCreateResult.Failure(input.Error);

            // This early check avoids opening a write transaction for an obviously invalid time.
            var time = BookingTimeRules.Validate(startUtc, durationMinutes, clock.UtcNow);
            if (!time.IsValid) return ReservationCreateResult.Failure(time.Error);

            try
            {
                using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
                {
                    var customer = db.Customers.SingleOrDefault(item => item.PhoneNumber == input.PhoneNumber);
                    if (customer == null)
                    {
                        customer = new Customer { FullName = input.FullName, PhoneNumber = input.PhoneNumber };
                        db.Customers.Add(customer);
                        db.SaveChanges();
                    }

                    // Re-read Room, Customer, Reservation and Session inside this transaction.
                    var availability = new AvailabilityService(db, clock)
                        .CheckReservation(roomId, customer.CustomerId, startUtc, durationMinutes);
                    if (!availability.IsAvailable)
                        return ReservationCreateResult.Failure(availability.Error);

                    var confirmedNow = clock.UtcNow;
                    var finalTime = BookingTimeRules.Validate(startUtc, durationMinutes, confirmedNow);
                    if (!finalTime.IsValid)
                        return ReservationCreateResult.Failure(finalTime.Error);

                    var reservation = new Reservation
                    {
                        RoomId = roomId,
                        CustomerId = customer.CustomerId,
                        StartTime = startUtc,
                        EndTime = availability.EndTimeUtc,
                        Status = ReservationStatuses.Confirmed,
                        CreatedAt = confirmedNow
                    };
                    db.Reservations.Add(reservation);
                    db.SaveChanges();

                    db.AuditLogs.Add(new AuditLog
                    {
                        ActorType = "Guest",
                        Action = "Create",
                        EntityName = "Reservation",
                        EntityId = reservation.ReservationId.ToString(),
                        Description = "Đặt trước phòng " + roomId,
                        CreatedAt = reservation.CreatedAt
                    });
                    db.SaveChanges();
                    transaction.Commit();
                    return ReservationCreateResult.Success(reservation.ReservationId);
                }
            }
            catch (Exception exception)
            {
                if (!IsConcurrentChange(exception)) throw;
                return ReservationCreateResult.Failure("Dữ liệu vừa thay đổi. Vui lòng kiểm tra lịch và thử lại.");
            }
        }

        private static bool IsConcurrentChange(Exception exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
            {
                var sql = current as SqlException;
                if (sql != null && (sql.Number == 1205 || sql.Number == 2601 || sql.Number == 2627))
                    return true;
            }
            return false;
        }
    }
}
