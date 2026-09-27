using System;
using System.Collections.Generic;

namespace MusicBoxManagement.Models
{
    public sealed class OrderLineChoiceViewModel
    {
        public int ServiceId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
    }

    public sealed class OrderItemSummaryViewModel
    {
        public string Name { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Amount { get { return Quantity * UnitPrice; } }
    }

    public sealed class OrderSummaryViewModel
    {
        public int OrderId { get; set; }
        public int RoomSessionId { get; set; }
        public string RoomName { get; set; }
        public string CustomerName { get; set; }
        public string Status { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public IList<OrderItemSummaryViewModel> Items { get; set; } = new List<OrderItemSummaryViewModel>();
        public decimal Amount
        {
            get
            {
                decimal total = 0;
                foreach (var item in Items) total += item.Amount;
                return total;
            }
        }
    }

    public sealed class StaffOrderCreateViewModel
    {
        public int RoomSessionId { get; set; }
        public string RoomName { get; set; }
        public string CustomerName { get; set; }
        public IList<OrderLineChoiceViewModel> Lines { get; set; } = new List<OrderLineChoiceViewModel>();
    }
}
