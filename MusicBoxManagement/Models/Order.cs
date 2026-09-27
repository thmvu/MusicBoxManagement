using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MusicBoxManagement.Models
{
    public static class OrderStatuses
    {
        public const string Pending = "Pending";
        public const string Completed = "Completed";
        public const string Cancelled = "Cancelled";
    }

    public class Order
    {
        public int OrderId { get; set; }
        public int RoomSessionId { get; set; }

        [StringLength(128)]
        public string CreatedByUserId { get; set; }

        [Required, StringLength(20)]
        public string Status { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
        public virtual RoomSession RoomSession { get; set; }
        public virtual ApplicationUser CreatedByUser { get; set; }
        public virtual ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    }

    public class OrderItem
    {
        public int OrderItemId { get; set; }
        public int OrderId { get; set; }
        public int ServiceId { get; set; }

        [Required, StringLength(100)]
        public string ServiceNameSnapshot { get; set; }

        [Range(1, 10)]
        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }
        public virtual Order Order { get; set; }
        public virtual Service Service { get; set; }
    }
}
