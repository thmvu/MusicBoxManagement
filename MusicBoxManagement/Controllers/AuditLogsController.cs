using System;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using MusicBoxManagement.Authorization;
using MusicBoxManagement.Models;

namespace MusicBoxManagement.Controllers
{
    [PermissionAuthorize("Audit.View")]
    public class AuditLogsController : Controller
    {
        public ActionResult Index(AuditLogSearchViewModel model)
        {
            model = model ?? new AuditLogSearchViewModel();
            if (model.Page < 1) model.Page = 1;
            if (model.Page > 100000) return new HttpStatusCodeResult(400);
            DateTimeOffset? from = null, to = null;
            if (!TryDate(model.FromDate, out from) || !TryDate(model.ToDate, out to))
                ModelState.AddModelError("", "Ngày lọc phải có dạng yyyy-MM-dd.");
            if (from.HasValue && to.HasValue && from > to)
                ModelState.AddModelError("", "Ngày bắt đầu không được sau ngày kết thúc.");
            if (!ModelState.IsValid) return View(model);

            using (var db = new ApplicationDbContext())
            {
                var query = db.AuditLogs.AsNoTracking().AsQueryable();
                if (!string.IsNullOrWhiteSpace(model.Action))
                {
                    var action = model.Action.Trim();
                    query = query.Where(item => item.Action.Contains(action));
                }
                if (!string.IsNullOrWhiteSpace(model.EntityName))
                {
                    var entity = model.EntityName.Trim();
                    query = query.Where(item => item.EntityName.Contains(entity));
                }
                if (from.HasValue)
                {
                    var start = from.Value;
                    query = query.Where(item => item.CreatedAt >= start);
                }
                if (to.HasValue && to.Value.Date < DateTime.MaxValue.Date)
                {
                    var end = to.Value.AddDays(1);
                    query = query.Where(item => item.CreatedAt < end);
                }
                var rows = query.OrderByDescending(item => item.CreatedAt)
                    .ThenByDescending(item => item.AuditLogId)
                    .Skip((model.Page - 1) * 50).Take(51)
                    .Select(item => new AuditLogItemViewModel
                    {
                        AuditLogId = item.AuditLogId,
                        CreatedAt = item.CreatedAt,
                        ActorType = item.ActorType,
                        UserName = item.User.UserName,
                        Action = item.Action,
                        EntityName = item.EntityName,
                        EntityId = item.EntityId,
                        Description = item.Description
                    }).ToList();
                model.HasNext = rows.Count > 50;
                model.Items = rows.Take(50).ToList();
                return View(model);
            }
        }

        private static bool TryDate(string input, out DateTimeOffset? result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(input)) return true;
            DateTime date;
            if (!DateTime.TryParseExact(input, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out date)) return false;
            result = new DateTimeOffset(date, TimeSpan.FromHours(7)).ToUniversalTime();
            return true;
        }
    }
}
