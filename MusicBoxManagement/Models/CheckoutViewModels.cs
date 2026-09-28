using System;
using System.Collections.Generic;
using MusicBoxManagement.Services;

namespace MusicBoxManagement.Models
{
    public sealed class CheckoutPreviewViewModel
    {
        public int RoomSessionId { get; set; }
        public string RoomCode { get; set; }
        public string RoomTypeName { get; set; }
        public string CustomerName { get; set; }
        public int PendingOrderCount { get; set; }
        public BillingPreview Billing { get; set; }
    }

    public sealed class InvoiceListItemViewModel
    {
        public int InvoiceId { get; set; }
        public string InvoiceNumber { get; set; }
        public string RoomCode { get; set; }
        public string CustomerName { get; set; }
        public DateTimeOffset PaidAt { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public sealed class InvoiceSearchViewModel
    {
        public string Code { get; set; }
        public string PhoneNumber { get; set; }
        public string FromDate { get; set; }
        public string ToDate { get; set; }
        public int Page { get; set; } = 1;
        public bool HasNext { get; set; }
        public IList<InvoiceListItemViewModel> Items { get; set; } = new List<InvoiceListItemViewModel>();
    }

    public sealed class InvoiceLineViewModel
    {
        public string Name { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Amount { get { return Quantity * UnitPrice; } }
    }

    public sealed class InvoiceDetailsViewModel
    {
        public int InvoiceId { get; set; }
        public string InvoiceNumber { get; set; }
        public int RoomSessionId { get; set; }
        public string RoomCode { get; set; }
        public string RoomTypeName { get; set; }
        public DateTimeOffset ActualStartTime { get; set; }
        public DateTimeOffset ActualEndTime { get; set; }
        public decimal HourlyRate { get; set; }
        public decimal UsedMinutes { get; set; }
        public decimal RoomCharge { get; set; }
        public decimal ServiceCharge { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentMethod { get; set; }
        public string ProcessedByName { get; set; }
        public DateTimeOffset PaidAt { get; set; }
        public IList<InvoiceLineViewModel> Items { get; set; } = new List<InvoiceLineViewModel>();
    }
}
