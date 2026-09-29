using System;
using System.Data.Entity;
using System.Linq;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using MusicBoxManagement.Models;

namespace MusicBoxManagement.Services
{
    public static class DevelopmentDemoSeeder
    {
        public static void Seed(ApplicationDbContext db, DateTimeOffset now, string demoPassword)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (string.IsNullOrWhiteSpace(demoPassword))
                throw new ArgumentException("Cần cấu hình mật khẩu demo qua MUSICBOX_DEMO_PASSWORD.", nameof(demoPassword));

            EnsureDemoUsers(db, demoPassword);
            var standard = db.RoomTypes.SingleOrDefault(item => item.Code == "STANDARD");
            var vip = db.RoomTypes.SingleOrDefault(item => item.Code == "VIP");
            if (standard == null || vip == null)
                throw new InvalidOperationException("Cần có loại phòng STANDARD và VIP trước khi seed dữ liệu demo.");

            EnsureRoom(db, "MB-101", "Phòng Standard 101", standard.RoomTypeId, true, null,
                "~/Content/images/landing-room.png", now);
            EnsureRoom(db, "MB-201", "Phòng VIP 201", vip.RoomTypeId, true, null,
                "~/Content/images/landing-room.png", now);
            EnsureRoom(db, "MB-999", "Phòng demo tạm khóa", standard.RoomTypeId, false,
                "Phòng minh họa trạng thái tạm khóa.", null, now);
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

        private static void EnsureDemoUsers(ApplicationDbContext db, string password)
        {
            var roleManager = new RoleManager<IdentityRole>(new RoleStore<IdentityRole>(db));
            var userManager = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(db));
            userManager.PasswordValidator = new PasswordValidator
            {
                RequiredLength = 6,
                RequireNonLetterOrDigit = true,
                RequireDigit = true,
                RequireLowercase = true,
                RequireUppercase = true
            };

            EnsureDemoUser(userManager, roleManager, "demo.staff", "Nhân viên demo", "Staff", password);
            EnsureDemoUser(userManager, roleManager, "demo.manager", "Quản lý demo", "Manager", password);
        }

        private static void EnsureDemoUser(UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager, string username, string fullName, string role,
            string password)
        {
            if (!roleManager.RoleExists(role))
                throw new InvalidOperationException("Cần có role " + role + " trước khi seed tài khoản demo.");

            var user = userManager.FindByName(username);
            if (user == null)
            {
                user = new ApplicationUser { UserName = username, FullName = fullName, IsActive = true };
                Check(userManager.Create(user, password));
                Check(userManager.AddToRole(user.Id, role));
                return;
            }

            if (!userManager.IsInRole(user.Id, role))
                throw new InvalidOperationException("Tài khoản " + username + " đã tồn tại nhưng không có role " + role + ".");
        }

        private static void EnsureRoom(ApplicationDbContext db, string code, string name,
            int roomTypeId, bool active, string reason, string imageUrl, DateTimeOffset now)
        {
            var room = db.Rooms.SingleOrDefault(item => item.RoomCode == code);
            if (room == null)
                db.Rooms.Add(new Room
                {
                    RoomCode = code, Name = name, RoomTypeId = roomTypeId,
                    IsActive = active, InactiveReason = reason, CreatedAt = now,
                    Description = "Dữ liệu demo cho đồ án Music Box.", ImageUrl = imageUrl
                });
            else if (active && string.IsNullOrWhiteSpace(room.ImageUrl))
                room.ImageUrl = imageUrl;
        }

        private static Customer EnsureCustomer(ApplicationDbContext db, string name, string phone)
        {
            var customer = db.Customers.SingleOrDefault(item => item.PhoneNumber == phone);
            if (customer != null) return customer;
            customer = new Customer { FullName = name, PhoneNumber = phone };
            db.Customers.Add(customer);
            return customer;
        }

        private static void Check(IdentityResult result)
        {
            if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors));
        }
    }
}
