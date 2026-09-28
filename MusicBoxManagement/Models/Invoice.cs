using System;
using System.ComponentModel.DataAnnotations;

namespace MusicBoxManagement.Models
{
    public static class PaymentMethods
    {
        public const string Cash = "Cash";
        public const string BankTransfer = "BankTransfer";
    }

    public class Invoice
    {
        public int InvoiceId { get; set; }

        [Required, StringLength(40)]
        public string InvoiceNumber { get; set; }

        public int RoomSessionId { get; set; }

        public decimal RoomCharge { get; set; }
        public decimal ServiceCharge { get; set; }
        public decimal TotalAmount { get; set; }

        [Required, StringLength(20)]
        public string PaymentMethod { get; set; }

        [Required, StringLength(128)]
        public string ProcessedByUserId { get; set; }

        [Required, StringLength(100)]
        public string ProcessedByNameSnapshot { get; set; }

        public DateTimeOffset PaidAt { get; set; }

        public virtual RoomSession RoomSession { get; set; }
        public virtual ApplicationUser ProcessedByUser { get; set; }
    }
}
