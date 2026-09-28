using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using MusicBoxManagement.Authorization;
using MusicBoxManagement.Models;
using MusicBoxManagement.Services;

namespace MusicBoxManagement.Controllers
{
    public class CheckoutController : Controller
    {
        [PermissionAuthorize("Session.CheckOut")]
        public ActionResult Index(int sessionId)
        {
            using (var db = new ApplicationDbContext())
            {
                var session = db.RoomSessions.AsNoTracking()
                    .Where(item => item.RoomSessionId == sessionId)
                    .Select(item => new CheckoutPreviewViewModel
                    {
                        RoomSessionId = item.RoomSessionId,
                        RoomCode = item.RoomCodeSnapshot,
                        RoomTypeName = item.RoomTypeNameSnapshot,
                        CustomerName = item.Customer.FullName
                    }).SingleOrDefault();
                if (session == null) return HttpNotFound();
                session.Billing = new CheckoutService(db, new SystemClock()).GetPreview(sessionId);
                if (session.Billing == null)
                {
                    var invoiceId = db.Invoices.AsNoTracking()
                        .Where(item => item.RoomSessionId == sessionId)
                        .Select(item => item.InvoiceId).SingleOrDefault();
                    if (invoiceId != 0 && new PermissionService(db)
                        .HasPermission(User.Identity.GetUserId(), "Invoice.View"))
                        return RedirectToAction("Details", "Invoices", new { id = invoiceId });
                    return new HttpStatusCodeResult(409, "Phiên đã kết thúc.");
                }
                session.PendingOrderCount = db.Orders.AsNoTracking().Count(item =>
                    item.RoomSessionId == sessionId && item.Status == OrderStatuses.Pending);
                return View(session);
            }
        }

        [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("Session.CheckOut")]
        public ActionResult Confirm(int sessionId, string paymentMethod)
        {
            using (var db = new ApplicationDbContext())
            {
                var result = new CheckoutService(db, new SystemClock())
                    .Confirm(sessionId, User.Identity.GetUserId(), paymentMethod);
                if (!result.Succeeded)
                {
                    TempData["Error"] = result.Error;
                    return RedirectToAction("Index", new { sessionId });
                }
                TempData["Success"] = "Đã nhận tiền và hoàn tất phiên. Hóa đơn đã được lưu.";
                if (new PermissionService(db).HasPermission(User.Identity.GetUserId(), "Invoice.View"))
                    return RedirectToAction("Details", "Invoices", new { id = result.InvoiceId });
                return RedirectToAction("Details", "RoomSessions", new { id = sessionId });
            }
        }
    }
}
