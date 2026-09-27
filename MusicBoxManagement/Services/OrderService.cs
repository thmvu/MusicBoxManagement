using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using MusicBoxManagement.Models;

namespace MusicBoxManagement.Services
{
    public sealed class OrderCreateResult
    {
        public bool Succeeded { get; private set; }
        public int OrderId { get; private set; }
        public string Error { get; private set; }

        public static OrderCreateResult Success(int orderId)
        {
            return new OrderCreateResult { Succeeded = true, OrderId = orderId };
        }

        public static OrderCreateResult Failure(string error)
        {
            return new OrderCreateResult { Error = error };
        }
    }

    public sealed class OrderTransitionResult
    {
        public bool Succeeded { get; private set; }
        public string Error { get; private set; }

        public static OrderTransitionResult Success()
        {
            return new OrderTransitionResult { Succeeded = true };
        }

        public static OrderTransitionResult Failure(string error)
        {
            return new OrderTransitionResult { Error = error };
        }
    }

    public sealed class OrderService
    {
        private readonly ApplicationDbContext db;
        private readonly IClock clock;

        public OrderService(ApplicationDbContext db, IClock clock)
        {
            this.db = db;
            this.clock = clock;
        }

        public OrderCreateResult CreateGuest(int sessionId, string phoneNumber, IEnumerable<OrderLineInput> lines)
        {
            string normalizedPhone;
            if (!PhoneNumberNormalizer.TryNormalize(phoneNumber, out normalizedPhone))
                return OrderCreateResult.Failure("Số điện thoại không hợp lệ.");
            return Create(sessionId, normalizedPhone, null, lines);
        }

        public OrderCreateResult CreateByStaff(int sessionId, string userId, IEnumerable<OrderLineInput> lines)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return OrderCreateResult.Failure("Thiếu nhân viên thực hiện.");
            return Create(sessionId, null, userId, lines);
        }

        public OrderTransitionResult ConfirmByStaff(int orderId, string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return OrderTransitionResult.Failure("Thiếu nhân viên thực hiện.");
            return Transition(orderId, null, userId, true);
        }

        public OrderTransitionResult CancelByStaff(int orderId, string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return OrderTransitionResult.Failure("Thiếu nhân viên thực hiện.");
            return Transition(orderId, null, userId, false);
        }

        public OrderTransitionResult CancelGuest(int orderId, string phoneNumber)
        {
            string normalizedPhone;
            if (!PhoneNumberNormalizer.TryNormalize(phoneNumber, out normalizedPhone))
                return OrderTransitionResult.Failure("Số điện thoại không hợp lệ.");
            return Transition(orderId, normalizedPhone, null, false);
        }

        private OrderTransitionResult Transition(int orderId, string guestPhone, string userId, bool confirm)
        {
            try
            {
                using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
                {
                    if (userId != null && !db.Users.Any(user => user.Id == userId && user.IsActive))
                        return OrderTransitionResult.Failure("Nhân viên không còn hoạt động.");
                    var order = db.Orders.Include("RoomSession.Customer")
                        .SingleOrDefault(item => item.OrderId == orderId);
                    if (order == null || (guestPhone != null && order.RoomSession.Customer.PhoneNumber != guestPhone))
                        return OrderTransitionResult.Failure("Không tìm thấy đơn món phù hợp.");
                    if (order.RoomSession.Status != RoomSessionStatuses.Active)
                        return OrderTransitionResult.Failure("Phiên đã kết thúc, không thể xử lý đơn món.");
                    if (order.Status != OrderStatuses.Pending)
                        return OrderTransitionResult.Failure("Trạng thái đơn đã thay đổi. Vui lòng tải lại.");

                    order.Status = confirm ? OrderStatuses.Completed : OrderStatuses.Cancelled;
                    db.AuditLogs.Add(new AuditLog
                    {
                        ActorType = userId == null ? "Guest" : "Staff",
                        UserId = userId,
                        Action = confirm ? "Confirm" : "Cancel",
                        EntityName = "Order",
                        EntityId = order.OrderId.ToString(),
                        Description = confirm ? "Nhân viên xác nhận món đã phục vụ." : "Hủy đơn món đang chờ.",
                        CreatedAt = clock.UtcNow
                    });
                    db.SaveChanges();
                    transaction.Commit();
                    return OrderTransitionResult.Success();
                }
            }
            catch (Exception error)
            {
                if (!IsConcurrentChange(error)) throw;
                return OrderTransitionResult.Failure("Dữ liệu vừa thay đổi. Vui lòng tải lại và thử lại.");
            }
        }

        private OrderCreateResult Create(int sessionId, string guestPhone, string userId,
            IEnumerable<OrderLineInput> lines)
        {
            var input = OrderInputRules.Validate(lines);
            if (!input.IsValid) return OrderCreateResult.Failure(input.Error);

            try
            {
                using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
                {
                    if (userId != null && !db.Users.Any(user => user.Id == userId && user.IsActive))
                        return OrderCreateResult.Failure("Nhân viên không còn hoạt động.");
                    var session = db.RoomSessions.Include("Customer")
                        .SingleOrDefault(item => item.RoomSessionId == sessionId);
                    if (session == null || (guestPhone != null && session.Customer.PhoneNumber != guestPhone))
                        return OrderCreateResult.Failure("Không tìm thấy phiên phù hợp.");
                    if (session.Status != RoomSessionStatuses.Active)
                        return OrderCreateResult.Failure("Phiên đã kết thúc, không thể gọi thêm món.");

                    var serviceIds = input.Lines.Select(item => item.ServiceId).ToList();
                    var services = db.Services.Where(item => serviceIds.Contains(item.ServiceId)).ToList();
                    if (services.Count != serviceIds.Count || services.Any(item => !item.IsActive ||
                        item.Price <= 0 || item.Price != Math.Floor(item.Price)))
                        return OrderCreateResult.Failure("Có món không còn bán hoặc giá không hợp lệ.");

                    var now = clock.UtcNow;
                    var localTime = now.ToOffset(TimeSpan.FromHours(7)).TimeOfDay;
                    var open = (localTime >= TimeSpan.FromHours(9) && localTime < TimeSpan.FromHours(12)) ||
                        (localTime >= TimeSpan.FromHours(13) && localTime < TimeSpan.FromHours(23));
                    if (!open) return OrderCreateResult.Failure("Hiện ngoài giờ nhận món mới.");

                    var order = new Order
                    {
                        RoomSessionId = sessionId,
                        CreatedByUserId = userId,
                        Status = userId == null ? OrderStatuses.Pending : OrderStatuses.Completed,
                        CreatedAt = now
                    };
                    foreach (var line in input.Lines)
                    {
                        var service = services.Single(item => item.ServiceId == line.ServiceId);
                        order.Items.Add(new OrderItem
                        {
                            ServiceId = service.ServiceId,
                            ServiceNameSnapshot = service.Name,
                            Quantity = line.Quantity,
                            UnitPrice = service.Price
                        });
                    }
                    db.Orders.Add(order);
                    db.SaveChanges();
                    db.AuditLogs.Add(new AuditLog
                    {
                        ActorType = userId == null ? "Guest" : "Staff",
                        UserId = userId,
                        Action = "Create",
                        EntityName = "Order",
                        EntityId = order.OrderId.ToString(),
                        Description = userId == null ? "Khách gửi yêu cầu món." : "Nhân viên ghi món đã phục vụ.",
                        CreatedAt = now
                    });
                    db.SaveChanges();
                    transaction.Commit();
                    return OrderCreateResult.Success(order.OrderId);
                }
            }
            catch (Exception error)
            {
                if (!IsConcurrentChange(error)) throw;
                return OrderCreateResult.Failure("Dữ liệu vừa thay đổi. Vui lòng tải lại và thử lại.");
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
