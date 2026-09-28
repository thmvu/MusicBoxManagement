using System;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using MusicBoxManagement.Authorization;
using MusicBoxManagement.Models;
using MusicBoxManagement.Services;

namespace MusicBoxManagement.Controllers
{
    public class InvoicesController : Controller
    {
        [PermissionAuthorize("Invoice.View")]
        public ActionResult Index(InvoiceSearchViewModel model)
        {
            model = model ?? new InvoiceSearchViewModel();
            if (model.Page < 1) model.Page = 1;
            if (model.Page > 100000) return new HttpStatusCodeResult(400);
            DateTimeOffset? from = null;
            DateTimeOffset? to = null;
            if (!TryDate(model.FromDate, out from) || !TryDate(model.ToDate, out to))
                ModelState.AddModelError("", "Ngày lọc phải có dạng yyyy-MM-dd.");
            if (from.HasValue && to.HasValue && from > to)
                ModelState.AddModelError("", "Ngày bắt đầu không được sau ngày kết thúc.");

            string phone = null;
            if (!string.IsNullOrWhiteSpace(model.PhoneNumber) &&
                !PhoneNumberNormalizer.TryNormalize(model.PhoneNumber, out phone))
                ModelState.AddModelError("", "Số điện thoại không hợp lệ.");
            if (!ModelState.IsValid) return View(model);

            using (var db = new ApplicationDbContext())
            {
                var query = db.Invoices.AsNoTracking().AsQueryable();
                if (!string.IsNullOrWhiteSpace(model.Code))
                {
                    var code = model.Code.Trim();
                    query = query.Where(item => item.InvoiceNumber.Contains(code));
                }
                if (phone != null)
                    query = query.Where(item => item.RoomSession.Customer.PhoneNumber == phone);
                if (from.HasValue)
                {
                    var start = from.Value;
                    query = query.Where(item => item.PaidAt >= start);
                }
                if (to.HasValue && to.Value.Date < DateTime.MaxValue.Date)
                {
                    var end = to.Value.AddDays(1);
                    query = query.Where(item => item.PaidAt < end);
                }
                var rows = query.OrderByDescending(item => item.PaidAt)
                    .Skip((model.Page - 1) * 25).Take(26)
                    .Select(item => new InvoiceListItemViewModel
                    {
                        InvoiceId = item.InvoiceId,
                        InvoiceNumber = item.InvoiceNumber,
                        RoomCode = item.RoomSession.RoomCodeSnapshot,
                        CustomerName = item.RoomSession.Customer.FullName,
                        PaidAt = item.PaidAt,
                        TotalAmount = item.TotalAmount
                    }).ToList();
                model.HasNext = rows.Count > 25;
                model.Items = rows.Take(25).ToList();
                return View(model);
            }
        }

        [PermissionAuthorize("Invoice.View")]
        public ActionResult Details(int id)
        {
            using (var db = new ApplicationDbContext())
            {
                var model = BuildDetails(db, id);
                if (model == null) return HttpNotFound();
                ViewBag.CanPrint = new PermissionService(db)
                    .HasPermission(User.Identity.GetUserId(), "Invoice.Print");
                return View(model);
            }
        }

        [PermissionAuthorize("Invoice.Print")]
        public ActionResult Print(int id)
        {
            using (var db = new ApplicationDbContext())
            {
                var model = BuildDetails(db, id);
                if (model == null) return HttpNotFound();
                return View(model);
            }
        }

        private static InvoiceDetailsViewModel BuildDetails(ApplicationDbContext db, int id)
        {
            var invoice = db.Invoices.AsNoTracking().Include("RoomSession")
                .SingleOrDefault(item => item.InvoiceId == id);
            if (invoice == null || !invoice.RoomSession.ActualEndTime.HasValue) return null;
            var session = invoice.RoomSession;
            var lines = db.OrderItems.AsNoTracking()
                .Where(item => item.Order.RoomSessionId == session.RoomSessionId &&
                    item.Order.Status == OrderStatuses.Completed)
                .OrderBy(item => item.OrderId).ThenBy(item => item.OrderItemId)
                .Select(item => new InvoiceLineViewModel
                {
                    Name = item.ServiceNameSnapshot,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice
                }).ToList();
            return new InvoiceDetailsViewModel
            {
                InvoiceId = invoice.InvoiceId,
                InvoiceNumber = invoice.InvoiceNumber,
                RoomSessionId = session.RoomSessionId,
                RoomCode = session.RoomCodeSnapshot,
                RoomTypeName = session.RoomTypeNameSnapshot,
                ActualStartTime = session.ActualStartTime,
                ActualEndTime = session.ActualEndTime.Value,
                HourlyRate = session.HourlyRate,
                UsedMinutes = (decimal)(session.ActualEndTime.Value - session.ActualStartTime).Ticks / TimeSpan.TicksPerMinute,
                RoomCharge = invoice.RoomCharge,
                ServiceCharge = invoice.ServiceCharge,
                TotalAmount = invoice.TotalAmount,
                PaymentMethod = invoice.PaymentMethod,
                ProcessedByName = invoice.ProcessedByNameSnapshot,
                PaidAt = invoice.PaidAt,
                Items = lines
            };
        }

        private static bool TryDate(string input, out DateTimeOffset? date)
        {
            date = null;
            if (string.IsNullOrWhiteSpace(input)) return true;
            DateTime value;
            if (!DateTime.TryParseExact(input, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out value)) return false;
            date = new DateTimeOffset(value, TimeSpan.FromHours(7)).ToUniversalTime();
            return true;
        }
    }
}
