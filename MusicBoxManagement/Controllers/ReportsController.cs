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
    public class ReportsController : Controller
    {
        [PermissionAuthorize("Report.View")]
        public ActionResult Index(ReportFilterViewModel model)
        {
            model = model ?? new ReportFilterViewModel();
            ReportQuery query;
            string error;
            if (!TryBuildQuery(model, out query, out error)) ModelState.AddModelError("", error);
            using (var db = new ApplicationDbContext())
            {
                model.Rooms = db.Rooms.AsNoTracking().OrderBy(item => item.RoomCode).ToList();
                model.RoomTypes = db.RoomTypes.AsNoTracking().OrderBy(item => item.Code).ToList();
                model.Services = db.Services.AsNoTracking().OrderBy(item => item.Name).ToList();
                model.CanExport = new PermissionService(db)
                    .HasPermission(User.Identity.GetUserId(), "Report.Export");
                if (ModelState.IsValid)
                {
                    if (!FilterExists(db, query)) ModelState.AddModelError("", "Phòng, loại phòng hoặc món đã chọn không tồn tại.");
                    else model.Data = new ReportService(db, new SystemClock()).GetReport(query);
                }
                return View(model);
            }
        }

        [PermissionAuthorize("Report.Export")]
        public ActionResult Export(ReportFilterViewModel model)
        {
            model = model ?? new ReportFilterViewModel();
            ReportQuery query;
            string error;
            if (!TryBuildQuery(model, out query, out error)) return new HttpStatusCodeResult(400, error);
            using (var db = new ApplicationDbContext())
            {
                if (!FilterExists(db, query)) return new HttpStatusCodeResult(400);
                var data = new ReportService(db, new SystemClock()).GetReport(query);
                var bytes = ReportExcelWriter.Write(data);
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "MusicBox-" + query.Kind + "-" + model.FromDate + "-" + model.ToDate + ".xlsx");
            }
        }

        private static bool TryBuildQuery(ReportFilterViewModel model, out ReportQuery query, out string error)
        {
            query = null;
            error = null;
            if (string.IsNullOrWhiteSpace(model.Kind)) model.Kind = ReportKinds.RevenueDay;
            if (!ReportKinds.All.Contains(model.Kind))
            {
                error = "Loại báo cáo không hợp lệ.";
                return false;
            }
            var today = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).Date;
            if (string.IsNullOrWhiteSpace(model.FromDate))
                model.FromDate = new DateTime(today.Year, today.Month, 1).ToString("yyyy-MM-dd");
            if (string.IsNullOrWhiteSpace(model.ToDate)) model.ToDate = today.ToString("yyyy-MM-dd");
            DateTime from, to;
            if (!DateTime.TryParseExact(model.FromDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out from) ||
                !DateTime.TryParseExact(model.ToDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out to) || from > to || to == DateTime.MaxValue.Date)
            {
                error = "Khoảng ngày không hợp lệ. Dùng yyyy-MM-dd và ngày bắt đầu không sau ngày kết thúc.";
                return false;
            }
            if (model.RoomId <= 0 || model.ServiceId <= 0)
            {
                error = "Bộ lọc phòng hoặc món không hợp lệ.";
                return false;
            }
            var offset = TimeSpan.FromHours(7);
            try
            {
                query = new ReportQuery
                {
                    Kind = model.Kind,
                    Start = new DateTimeOffset(from, offset).ToUniversalTime(),
                    End = new DateTimeOffset(to.AddDays(1), offset).ToUniversalTime(),
                    RoomTypeCode = string.IsNullOrWhiteSpace(model.RoomTypeCode) ? null : model.RoomTypeCode.Trim(),
                    RoomId = model.RoomId, ServiceId = model.ServiceId
                };
            }
            catch (ArgumentOutOfRangeException)
            {
                error = "Khoảng ngày nằm ngoài giới hạn thời gian hỗ trợ.";
                return false;
            }
            return true;
        }

        private static bool FilterExists(ApplicationDbContext db, ReportQuery query)
        {
            if (query.Kind == ReportKinds.RevenueRoomType && query.RoomTypeCode != null &&
                !db.RoomTypes.Any(item => item.Code == query.RoomTypeCode)) return false;
            if ((query.Kind == ReportKinds.RoomUsage || query.Kind == ReportKinds.Utilization) &&
                query.RoomId.HasValue && !db.Rooms.Any(item => item.RoomId == query.RoomId.Value)) return false;
            if (query.Kind == ReportKinds.ServiceSales && query.ServiceId.HasValue &&
                !db.Services.Any(item => item.ServiceId == query.ServiceId.Value)) return false;
            return true;
        }
    }
}
