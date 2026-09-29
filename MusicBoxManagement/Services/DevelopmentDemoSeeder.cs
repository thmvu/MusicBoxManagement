using System;
using System.Data.Entity;
using System.Linq;
using MusicBoxManagement.Models;

namespace MusicBoxManagement.Services
{
    public static class DevelopmentDemoSeeder
    {
        public static void Seed(ApplicationDbContext db, DateTimeOffset now)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            var standard = db.RoomTypes.SingleOrDefault(item => item.Code == "STANDARD");
            var vip = db.RoomTypes.SingleOrDefault(item => item.Code == "VIP");
            if (standard == null || vip == null)
                throw new InvalidOperationException("Cần có loại phòng STANDARD và VIP trước khi seed dữ liệu demo.");

            EnsureRoom(db, "MB-101", "Phòng Standard 101", standard.RoomTypeId, true, null, now);
            EnsureRoom(db, "MB-201", "Phòng VIP 201", vip.RoomTypeId, true, null, now);
            EnsureRoom(db, "MB-999", "Phòng demo tạm khóa", standard.RoomTypeId, false,
                "Phòng minh họa trạng thái tạm khóa.", now);
            db.SaveChanges();

            var bookingCustomer = EnsureCustomer(db, "Khách đặt trước demo", "0900000001");
            var activeCustomer = EnsureCustomer(db, "Khách đang sử dụng demo", "0900000002");
            db.SaveChanges();

            var bookingRoomId = db.Rooms.Where(item => item.RoomCode == "MB-101")
                .Select(item => item.RoomId).Single();
            var activeRoom = db.Rooms.Single(item => item.RoomCode == "MB-201");
            var offset = TimeSpan.FromHours(7);
            var localTomorrow = now.ToOffset(offset).Date.AddDays(1);
            var bookingStart = new DateTimeOffset(localTomorrow.AddHours(19), offset).ToUniversalTime();
            if (!db.Reservations.Any(item => item.CustomerId == bookingCustomer.CustomerId &&
                item.RoomId == bookingRoomId && item.Status == ReservationStatuses.Confirmed))
            {
                db.Reservations.Add(new Reservation
                {
                    CustomerId = bookingCustomer.CustomerId, RoomId = bookingRoomId,
                    StartTime = bookingStart, EndTime = bookingStart.AddHours(2),
                    Status = ReservationStatuses.Confirmed, CreatedAt = now
                });
            }
            if (!db.RoomSessions.Any(item => item.CustomerId == activeCustomer.CustomerId &&
                item.Status == RoomSessionStatuses.Active))
            {
                db.RoomSessions.Add(new RoomSession
                {
                    CustomerId = activeCustomer.CustomerId, RoomId = activeRoom.RoomId,
                    ActualStartTime = now.AddMinutes(-45),
                    HourlyRate = vip.PricePerHour, RoomCodeSnapshot = activeRoom.RoomCode,
                    RoomTypeCodeSnapshot = vip.Code, RoomTypeNameSnapshot = vip.Name,
                    Status = RoomSessionStatuses.Active
                });
            }
            db.SaveChanges();
        }

        private static void EnsureRoom(ApplicationDbContext db, string code, string name,
            int roomTypeId, bool active, string reason, DateTimeOffset now)
        {
            if (!db.Rooms.Any(item => item.RoomCode == code))
                db.Rooms.Add(new Room
                {
                    RoomCode = code, Name = name, RoomTypeId = roomTypeId,
                    IsActive = active, InactiveReason = reason, CreatedAt = now,
                    Description = "Dữ liệu demo cho đồ án Music Box."
                });
        }

        private static Customer EnsureCustomer(ApplicationDbContext db, string name, string phone)
        {
            var customer = db.Customers.SingleOrDefault(item => item.PhoneNumber == phone);
            if (customer != null) return customer;
            customer = new Customer { FullName = name, PhoneNumber = phone };
            db.Customers.Add(customer);
            return customer;
        }
    }
}
