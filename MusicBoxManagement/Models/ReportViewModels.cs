using System;
using System.Collections.Generic;
using System.Globalization;

namespace MusicBoxManagement.Models
{
    public static class ReportKinds
    {
        public const string RevenueDay = "revenue-day";
        public const string RevenueMonth = "revenue-month";
        public const string RevenueRoomType = "revenue-roomtype";
        public const string RoomUsage = "room-usage";
        public const string Utilization = "utilization";
        public const string BookingStatus = "booking-status";
        public const string ServiceSales = "service-sales";
        public static readonly string[] All = { RevenueDay, RevenueMonth, RevenueRoomType,
            RoomUsage, Utilization, BookingStatus, ServiceSales };
    }

    public sealed class ReportFilterViewModel
    {
        public string Kind { get; set; }
        public string FromDate { get; set; }
        public string ToDate { get; set; }
        public string RoomTypeCode { get; set; }
        public int? RoomId { get; set; }
        public int? ServiceId { get; set; }
        public bool CanExport { get; set; }
        public IList<Room> Rooms { get; set; } = new List<Room>();
        public IList<RoomType> RoomTypes { get; set; } = new List<RoomType>();
        public IList<Service> Services { get; set; } = new List<Service>();
        public ReportData Data { get; set; }
    }

    public sealed class ReportQuery
    {
        public string Kind { get; set; }
        public DateTimeOffset Start { get; set; }
        public DateTimeOffset End { get; set; }
        public string RoomTypeCode { get; set; }
        public int? RoomId { get; set; }
        public int? ServiceId { get; set; }
    }

    public sealed class ReportData
    {
        public string Title { get; set; }
        public string TimeBasis { get; set; }
        public string FilterDescription { get; set; }
        public DateTimeOffset GeneratedAt { get; set; }
        public IList<string> Headers { get; set; } = new List<string>();
        public IList<ReportRow> Rows { get; set; } = new List<ReportRow>();
        public ReportRow Total { get; set; }
    }

    public sealed class ReportRow
    {
        public IList<ReportCell> Cells { get; set; } = new List<ReportCell>();
        public static ReportRow Of(params ReportCell[] cells) { return new ReportRow { Cells = cells }; }
    }

    public sealed class ReportCell
    {
        public string Display { get; set; }
        public decimal? Number { get; set; }
        public static ReportCell Text(string value) { return new ReportCell { Display = value ?? "" }; }
        public static ReportCell Amount(decimal value) { return new ReportCell { Display = value.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")), Number = value }; }
        public static ReportCell Count(int value) { return new ReportCell { Display = value.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")), Number = value }; }
        public static ReportCell Ratio(decimal value) { return new ReportCell { Display = value.ToString("N1", CultureInfo.GetCultureInfo("vi-VN")) + "%", Number = value }; }
        public static ReportCell Minutes(double value) { return new ReportCell { Display = value.ToString("N1", CultureInfo.GetCultureInfo("vi-VN")), Number = (decimal)value }; }
    }
}
