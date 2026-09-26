using System;
using System.Data;
using System.Data.SqlClient;
using System.Collections.Generic;
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

    public sealed class ReservationCancelResult
    {
        public bool Succeeded { get; private set; }
        public string Error { get; private set; }

        public static ReservationCancelResult Success() { return new ReservationCancelResult { Succeeded = true }; }
        public static ReservationCancelResult Failure(string error) { return new ReservationCancelResult { Error = error }; }
    }

    public sealed class GuestBookingSummary
    {
        public int ReservationId { get; set; }
        public string RoomName { get; set; }
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }
        public bool CanCancel { get; set; }
    }

    public sealed class GuestLookupResult
    {
        public bool IsValid { get; set; }
        public IList<GuestBookingSummary> Bookings { get; set; }
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

        public ReservationCancelResult CancelGuest(int reservationId, string phoneNumber)
        {
            string normalizedPhone;
            if (!PhoneNumberNormalizer.TryNormalize(phoneNumber, out normalizedPhone))
                return ReservationCancelResult.Failure("Số điện thoại không hợp lệ.");

            return Cancel(reservationId, normalizedPhone, null, null);
        }

        public GuestLookupResult LookupGuest(string phoneNumber)
        {
            string normalizedPhone;
            if (!PhoneNumberNormalizer.TryNormalize(phoneNumber, out normalizedPhone))
                return new GuestLookupResult { Bookings = new List<GuestBookingSummary>() };

            var now = clock.UtcNow;
            var earliestStart = now.AddMinutes(-15);
            var bookings = db.Reservations
                .Where(item => item.Customer.PhoneNumber == normalizedPhone &&
                    item.Status == ReservationStatuses.Confirmed && item.StartTime > earliestStart)
                .OrderBy(item => item.StartTime)
                .Select(item => new { item.ReservationId, item.Room.Name, item.StartTime, item.EndTime })
                .ToList()
                .Select(item => new GuestBookingSummary
                {
                    ReservationId = item.ReservationId,
                    RoomName = item.Name,
                    StartTime = item.StartTime,
                    EndTime = item.EndTime,
                    CanCancel = now <= item.StartTime.AddHours(-2)
                }).ToList();
            return new GuestLookupResult { IsValid = true, Bookings = bookings };
        }

        public ReservationCancelResult CancelByStaff(int reservationId, string reason, string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return ReservationCancelResult.Failure("Thiếu nhân viên thực hiện.");
            if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
                return ReservationCancelResult.Failure("Vui lòng nhập lý do hủy (tối đa 500 ký tự).");

            return Cancel(reservationId, null, reason.Trim(), userId);
        }

        private ReservationCancelResult Cancel(int reservationId, string guestPhone, string staffReason, string userId)
        {
            try
            {
                using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
                {
                    var reservation = db.Reservations.Include("Customer")
                        .SingleOrDefault(item => item.ReservationId == reservationId);
                    if (reservation == null || (guestPhone != null && reservation.Customer.PhoneNumber != guestPhone))
                        return ReservationCancelResult.Failure("Không tìm thấy đặt phòng phù hợp.");

                    var now = clock.UtcNow;
                    if (reservation.Status != ReservationStatuses.Confirmed ||
                        now >= reservation.StartTime.AddMinutes(15))
                        return ReservationCancelResult.Failure("Đặt phòng không còn hiệu lực để hủy.");
                    if (guestPhone != null && now > reservation.StartTime.AddHours(-2))
                        return ReservationCancelResult.Failure("Đã quá thời hạn hủy online. Vui lòng liên hệ cửa hàng.");

                    reservation.Status = ReservationStatuses.Cancelled;
                    reservation.CancellationReason = guestPhone != null
                        ? "Customer cancelled online" : staffReason;
                    db.AuditLogs.Add(new AuditLog
                    {
                        ActorType = guestPhone != null ? "Guest" : "Staff",
                        UserId = userId,
                        Action = "Cancel",
                        EntityName = "Reservation",
                        EntityId = reservation.ReservationId.ToString(),
                        Description = reservation.CancellationReason,
                        CreatedAt = now
                    });
                    db.SaveChanges();
                    transaction.Commit();
                    return ReservationCancelResult.Success();
                }
            }
            catch (Exception exception)
            {
                if (!IsConcurrentChange(exception)) throw;
                return ReservationCancelResult.Failure("Dữ liệu vừa thay đổi. Vui lòng thử lại.");
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
