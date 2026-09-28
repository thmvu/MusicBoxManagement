using System;
using System.Data;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using MusicBoxManagement.Models;

namespace MusicBoxManagement.Services
{
    public sealed class CheckoutResult
    {
        public bool Succeeded { get; private set; }
        public int InvoiceId { get; private set; }
        public string Error { get; private set; }

        public static CheckoutResult Success(int invoiceId)
        {
            return new CheckoutResult { Succeeded = true, InvoiceId = invoiceId };
        }

        public static CheckoutResult Failure(string error)
        {
            return new CheckoutResult { Error = error };
        }
    }

    public sealed class CheckoutService
    {
        private readonly ApplicationDbContext db;
        private readonly IClock clock;

        public CheckoutService(ApplicationDbContext db, IClock clock)
        {
            this.db = db;
            this.clock = clock;
        }

        public BillingPreview GetPreview(int sessionId)
        {
            return new BillingService(db, clock).GetPreview(sessionId);
        }

        public CheckoutResult Confirm(int sessionId, string userId, string paymentMethod)
        {
            try
            {
                using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
                {
                    var existingInvoice = db.Invoices.AsNoTracking()
                        .Where(item => item.RoomSessionId == sessionId)
                        .Select(item => item.InvoiceId).SingleOrDefault();
                    if (existingInvoice != 0) return CheckoutResult.Success(existingInvoice);
                    if (paymentMethod != PaymentMethods.Cash && paymentMethod != PaymentMethods.BankTransfer)
                        return CheckoutResult.Failure("Chọn tiền mặt hoặc chuyển khoản.");
                    if (string.IsNullOrWhiteSpace(userId))
                        return CheckoutResult.Failure("Thiếu nhân viên xác nhận.");
                    var user = db.Users.SingleOrDefault(item => item.Id == userId && item.IsActive);
                    if (user == null) return CheckoutResult.Failure("Nhân viên không còn hoạt động.");

                    var session = db.RoomSessions.Include("Reservation")
                        .SingleOrDefault(item => item.RoomSessionId == sessionId);
                    if (session == null) return CheckoutResult.Failure("Không tìm thấy phiên sử dụng.");
                    if (session.Status != RoomSessionStatuses.Active)
                        return CheckoutResult.Failure("Phiên đã kết thúc.");
                    if (session.Reservation != null && session.Reservation.Status != ReservationStatuses.CheckedIn)
                        return CheckoutResult.Failure("Trạng thái đặt phòng không phù hợp để chốt.");

                    var orders = db.Orders.Include("Items")
                        .Where(item => item.RoomSessionId == sessionId).ToList();
                    var serviceCharge = orders.Where(item => item.Status == OrderStatuses.Completed)
                        .SelectMany(item => item.Items).Sum(item => item.Quantity * item.UnitPrice);
                    var checkoutNow = clock.UtcNow;
                    if (checkoutNow < session.ActualStartTime)
                        return CheckoutResult.Failure("Thời gian kết thúc không hợp lệ.");
                    var final = BillingService.Calculate(session.ActualStartTime,
                        session.HourlyRate, serviceCharge, checkoutNow);

                    foreach (var order in orders.Where(item => item.Status == OrderStatuses.Pending))
                        order.Status = OrderStatuses.Cancelled;
                    session.ActualEndTime = checkoutNow;
                    session.Status = RoomSessionStatuses.Completed;
                    if (session.Reservation != null)
                        session.Reservation.Status = ReservationStatuses.Completed;

                    var invoice = new Invoice
                    {
                        InvoiceNumber = "MB-" + Guid.NewGuid().ToString("N").ToUpperInvariant(),
                        RoomSessionId = sessionId,
                        RoomCharge = final.RoomCharge,
                        ServiceCharge = final.ServiceCharge,
                        TotalAmount = final.TotalAmount,
                        PaymentMethod = paymentMethod,
                        ProcessedByUserId = userId,
                        ProcessedByNameSnapshot = user.FullName,
                        PaidAt = checkoutNow
                    };
                    db.Invoices.Add(invoice);
                    db.SaveChanges();
                    db.AuditLogs.Add(new AuditLog
                    {
                        ActorType = "Staff",
                        UserId = userId,
                        Action = "Checkout",
                        EntityName = "Invoice",
                        EntityId = invoice.InvoiceId.ToString(),
                        Description = "Đã nhận tiền và hoàn tất phiên #" + sessionId + ".",
                        CreatedAt = checkoutNow
                    });
                    db.SaveChanges();
                    transaction.Commit();
                    return CheckoutResult.Success(invoice.InvoiceId);
                }
            }
            catch (Exception error)
            {
                if (!IsConcurrentChange(error)) throw;
                var existing = db.Invoices.AsNoTracking()
                    .Where(item => item.RoomSessionId == sessionId)
                    .Select(item => item.InvoiceId).SingleOrDefault();
                return existing != 0 ? CheckoutResult.Success(existing)
                    : CheckoutResult.Failure("Dữ liệu vừa thay đổi. Vui lòng tải lại và thử lại.");
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
