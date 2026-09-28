using System;
using System.Collections.Generic;

namespace MusicBoxManagement.Models
{
    public sealed class CustomerDetailsViewModel
    {
        public int CustomerId { get; set; }
        public string FullName { get; set; }
        public string PhoneNumber { get; set; }
        public int CompletedSessionCount { get; set; }
        public DateTimeOffset? LastUsedAt { get; set; }
        public bool CanEdit { get; set; }
        public bool CanViewReservations { get; set; }
        public bool CanViewSessions { get; set; }
        public bool CanViewInvoices { get; set; }
        public IList<CustomerReservationHistoryItem> Reservations { get; set; } = new List<CustomerReservationHistoryItem>();
        public IList<CustomerSessionHistoryItem> Sessions { get; set; } = new List<CustomerSessionHistoryItem>();
        public IList<CustomerInvoiceHistoryItem> Invoices { get; set; } = new List<CustomerInvoiceHistoryItem>();
    }

    public sealed class CustomerReservationHistoryItem
    {
        public int ReservationId { get; set; }
        public string RoomCode { get; set; }
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }
        public string Status { get; set; }
    }

    public sealed class CustomerSessionHistoryItem
    {
        public int RoomSessionId { get; set; }
        public string RoomCode { get; set; }
        public DateTimeOffset ActualStartTime { get; set; }
        public DateTimeOffset? ActualEndTime { get; set; }
        public string Status { get; set; }
    }

    public sealed class CustomerInvoiceHistoryItem
    {
        public int InvoiceId { get; set; }
        public string InvoiceNumber { get; set; }
        public string RoomCode { get; set; }
        public DateTimeOffset PaidAt { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
