using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using MusicBoxManagement.Models;

namespace MusicBoxManagement.Services
{
    public sealed class ReportService
    {
        private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);
        private readonly ApplicationDbContext db;
        private readonly IClock clock;

        public ReportService(ApplicationDbContext db, IClock clock)
        {
            this.db = db;
            this.clock = clock;
        }

        public ReportData GetReport(ReportQuery query)
        {
            if (query == null || !ReportKinds.All.Contains(query.Kind) || query.End <= query.Start)
                throw new ArgumentException("Bộ lọc báo cáo không hợp lệ.");
            var vi = CultureInfo.GetCultureInfo("vi-VN");
            var result = new ReportData
            {
                GeneratedAt = clock.UtcNow,
                FilterDescription = "Từ " + query.Start.ToOffset(VietnamOffset).ToString("dd/MM/yyyy", vi) +
                    " đến " + query.End.AddTicks(-1).ToOffset(VietnamOffset).ToString("dd/MM/yyyy", vi)
            };
            switch (query.Kind)
            {
                case ReportKinds.RevenueDay: RevenueByPeriod(query, result, false); break;
                case ReportKinds.RevenueMonth: RevenueByPeriod(query, result, true); break;
                case ReportKinds.RevenueRoomType: RevenueByRoomType(query, result); break;
                case ReportKinds.RoomUsage: RoomUsage(query, result); break;
                case ReportKinds.Utilization: Utilization(query, result); break;
                case ReportKinds.BookingStatus: BookingStatus(query, result); break;
                case ReportKinds.ServiceSales: ServiceSales(query, result); break;
            }
            return result;
        }

        private void RevenueByPeriod(ReportQuery query, ReportData result, bool monthly)
        {
            result.Title = monthly ? "Doanh thu theo tháng" : "Doanh thu theo ngày";
            result.TimeBasis = "Lọc theo ngày thanh toán hóa đơn (PaidAt).";
            result.Headers = new[] { monthly ? "Tháng" : "Ngày", "Số hóa đơn", "Tiền phòng (đ)", "Tiền món (đ)", "Tổng (đ)" };
            var invoices = db.Invoices.AsNoTracking()
                .Where(item => item.PaidAt >= query.Start && item.PaidAt < query.End)
                .Select(item => new { item.PaidAt, item.RoomCharge, item.ServiceCharge, item.TotalAmount }).ToList();
            var groups = invoices.GroupBy(item => monthly
                ? item.PaidAt.ToOffset(VietnamOffset).ToString("yyyy-MM")
                : item.PaidAt.ToOffset(VietnamOffset).ToString("yyyy-MM-dd"))
                .OrderBy(item => item.Key);
            foreach (var group in groups)
                result.Rows.Add(ReportRow.Of(ReportCell.Text(group.Key), ReportCell.Count(group.Count()),
                    ReportCell.Amount(group.Sum(item => item.RoomCharge)),
                    ReportCell.Amount(group.Sum(item => item.ServiceCharge)),
                    ReportCell.Amount(group.Sum(item => item.TotalAmount))));
            result.Total = ReportRow.Of(ReportCell.Text("Tổng"), ReportCell.Count(invoices.Count),
                ReportCell.Amount(invoices.Sum(item => item.RoomCharge)),
                ReportCell.Amount(invoices.Sum(item => item.ServiceCharge)),
                ReportCell.Amount(invoices.Sum(item => item.TotalAmount)));
        }

        private void RevenueByRoomType(ReportQuery query, ReportData result)
        {
            result.Title = "Doanh thu theo loại phòng";
            result.TimeBasis = "Lọc theo PaidAt; loại phòng là snapshot khi nhận phòng.";
            result.Headers = new[] { "Mã loại", "Tên lúc nhận phòng", "Số hóa đơn", "Tiền phòng (đ)", "Tiền món (đ)", "Tổng (đ)" };
            var invoices = db.Invoices.AsNoTracking()
                .Where(item => item.PaidAt >= query.Start && item.PaidAt < query.End &&
                    (query.RoomTypeCode == null || item.RoomSession.RoomTypeCodeSnapshot == query.RoomTypeCode))
                .Select(item => new { item.RoomSession.RoomTypeCodeSnapshot,
                    item.RoomSession.RoomTypeNameSnapshot, item.RoomCharge, item.ServiceCharge, item.TotalAmount }).ToList();
            foreach (var group in invoices.GroupBy(item => item.RoomTypeCodeSnapshot)
                .OrderBy(item => item.Key))
                result.Rows.Add(ReportRow.Of(ReportCell.Text(group.Key),
                    ReportCell.Text(string.Join(" / ", group.Select(item => item.RoomTypeNameSnapshot)
                        .Distinct().OrderBy(item => item))), ReportCell.Count(group.Count()),
                    ReportCell.Amount(group.Sum(item => item.RoomCharge)),
                    ReportCell.Amount(group.Sum(item => item.ServiceCharge)),
                    ReportCell.Amount(group.Sum(item => item.TotalAmount))));
            result.Total = ReportRow.Of(ReportCell.Text("Tổng"), ReportCell.Text(""),
                ReportCell.Count(invoices.Count), ReportCell.Amount(invoices.Sum(item => item.RoomCharge)),
                ReportCell.Amount(invoices.Sum(item => item.ServiceCharge)),
                ReportCell.Amount(invoices.Sum(item => item.TotalAmount)));
            if (query.RoomTypeCode != null) result.FilterDescription += " · Loại " + query.RoomTypeCode;
        }

        private void RoomUsage(ReportQuery query, ReportData result)
        {
            result.Title = "Số lượt sử dụng phòng";
            result.TimeBasis = "Đếm phiên Completed theo giờ trả phòng thực tế (ActualEndTime).";
            result.Headers = new[] { "Phòng", "Số lượt hoàn tất" };
            var rooms = db.Rooms.AsNoTracking().Where(item =>
                !query.RoomId.HasValue || item.RoomId == query.RoomId.Value)
                .OrderBy(item => item.RoomCode).Select(item => new { item.RoomId, item.RoomCode }).ToList();
            var counts = db.RoomSessions.AsNoTracking()
                .Where(item => item.Status == RoomSessionStatuses.Completed &&
                    item.ActualEndTime >= query.Start && item.ActualEndTime < query.End &&
                    (!query.RoomId.HasValue || item.RoomId == query.RoomId.Value))
                .GroupBy(item => item.RoomId).Select(item => new { RoomId = item.Key, Count = item.Count() })
                .ToDictionary(item => item.RoomId, item => item.Count);
            foreach (var room in rooms)
                result.Rows.Add(ReportRow.Of(ReportCell.Text(room.RoomCode),
                    ReportCell.Count(counts.ContainsKey(room.RoomId) ? counts[room.RoomId] : 0)));
            result.Total = ReportRow.Of(ReportCell.Text("Tổng"), ReportCell.Count(counts.Values.Sum()));
            if (query.RoomId.HasValue) result.FilterDescription += " · Phòng " + rooms.Select(item => item.RoomCode).FirstOrDefault();
        }

        private void Utilization(ReportQuery query, ReportData result)
        {
            result.Title = "Tỷ lệ sử dụng phòng";
            result.TimeBasis = "Theo lịch mở cửa tiêu chuẩn 09:00–12:00, 13:00–23:00; không trừ thời gian phòng từng bị khóa.";
            result.Headers = new[] { "Phòng", "Phút sử dụng", "Phút mở cửa chuẩn", "Tỷ lệ (%)" };
            var end = query.End < clock.UtcNow ? query.End : clock.UtcNow;
            var rooms = db.Rooms.AsNoTracking()
                .Where(item => !query.RoomId.HasValue || item.RoomId == query.RoomId.Value)
                .OrderBy(item => item.RoomCode)
                .Select(item => new { item.RoomId, item.RoomCode, item.CreatedAt }).ToList();
            var sessions = db.RoomSessions.AsNoTracking()
                .Where(item => item.ActualStartTime < end &&
                    (item.Status == RoomSessionStatuses.Active ||
                     (item.Status == RoomSessionStatuses.Completed && item.ActualEndTime > query.Start)) &&
                    (!query.RoomId.HasValue || item.RoomId == query.RoomId.Value))
                .Select(item => new { item.RoomId, item.ActualStartTime, item.ActualEndTime, item.Status }).ToList();
            foreach (var room in rooms)
            {
                var start = room.CreatedAt > query.Start ? room.CreatedAt : query.Start;
                var opening = ReportMath.OpeningMinutes(start, end);
                var used = sessions.Where(item => item.RoomId == room.RoomId)
                    .Sum(item => ReportMath.UsedMinutes(item.ActualStartTime,
                        item.Status == RoomSessionStatuses.Active ? end : item.ActualEndTime.Value, start, end));
                used = Math.Min(used, opening);
                result.Rows.Add(ReportRow.Of(ReportCell.Text(room.RoomCode), ReportCell.Minutes(used),
                    ReportCell.Minutes(opening), opening > 0
                        ? ReportCell.Ratio((decimal)(used / opening * 100)) : ReportCell.Text("Không có dữ liệu")));
            }
            if (query.RoomId.HasValue) result.FilterDescription += " · Phòng " + rooms.Select(item => item.RoomCode).FirstOrDefault();
        }

        private void BookingStatus(ReportQuery query, ReportData result)
        {
            result.Title = "Đặt phòng theo trạng thái";
            result.TimeBasis = "Lọc theo giờ bắt đầu đặt phòng (StartTime); Confirmed quá 15 phút hiện là NoShow.";
            result.Headers = new[] { "Trạng thái", "Số đặt phòng" };
            var bookings = db.Reservations.AsNoTracking()
                .Where(item => item.StartTime >= query.Start && item.StartTime < query.End)
                .Select(item => new { item.StartTime, item.Status }).ToList();
            var now = clock.UtcNow;
            var counts = bookings.GroupBy(item => item.Status == ReservationStatuses.Confirmed &&
                now >= item.StartTime.AddMinutes(15) ? ReservationStatuses.NoShow : item.Status)
                .ToDictionary(item => item.Key, item => item.Count());
            var labels = new[] {
                new { Code = ReservationStatuses.Confirmed, Label = "Đã xác nhận" },
                new { Code = ReservationStatuses.CheckedIn, Label = "Đã nhận phòng" },
                new { Code = ReservationStatuses.Completed, Label = "Đã hoàn tất" },
                new { Code = ReservationStatuses.Cancelled, Label = "Đã hủy" },
                new { Code = ReservationStatuses.NoShow, Label = "Không đến" }
            };
            foreach (var status in labels)
                result.Rows.Add(ReportRow.Of(ReportCell.Text(status.Label),
                    ReportCell.Count(counts.ContainsKey(status.Code) ? counts[status.Code] : 0)));
            result.Total = ReportRow.Of(ReportCell.Text("Tổng"), ReportCell.Count(bookings.Count));
        }

        private void ServiceSales(ReportQuery query, ReportData result)
        {
            result.Title = "Doanh thu món";
            result.TimeBasis = "Chỉ món của Order Completed thuộc phiên có hóa đơn; lọc theo Invoice.PaidAt, nhóm theo ServiceId.";
            result.Headers = new[] { "Mã món", "Tên món lúc gọi", "Số lượng", "Doanh thu (đ)" };
            var items = (from line in db.OrderItems.AsNoTracking()
                         join order in db.Orders on line.OrderId equals order.OrderId
                         join invoice in db.Invoices on order.RoomSessionId equals invoice.RoomSessionId
                         where order.Status == OrderStatuses.Completed &&
                             invoice.PaidAt >= query.Start && invoice.PaidAt < query.End &&
                             (!query.ServiceId.HasValue || line.ServiceId == query.ServiceId.Value)
                         select new { line.ServiceId, line.ServiceNameSnapshot, line.Quantity, line.UnitPrice })
                         .ToList();
            foreach (var group in items.GroupBy(item => item.ServiceId)
                .OrderByDescending(item => item.Sum(line => line.Quantity * line.UnitPrice)))
                result.Rows.Add(ReportRow.Of(ReportCell.Count(group.Key),
                    ReportCell.Text(string.Join(" / ", group.Select(item => item.ServiceNameSnapshot)
                        .Distinct().OrderBy(item => item))),
                    ReportCell.Count(group.Sum(item => item.Quantity)),
                    ReportCell.Amount(group.Sum(item => item.Quantity * item.UnitPrice))));
            result.Total = ReportRow.Of(ReportCell.Text("Tổng"), ReportCell.Text(""),
                ReportCell.Count(items.Sum(item => item.Quantity)),
                ReportCell.Amount(items.Sum(item => item.Quantity * item.UnitPrice)));
            if (query.ServiceId.HasValue) result.FilterDescription += " · Mã món " + query.ServiceId;
        }
    }
}
