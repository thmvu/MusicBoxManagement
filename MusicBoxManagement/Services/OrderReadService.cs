using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using MusicBoxManagement.Models;

namespace MusicBoxManagement.Services
{
    public sealed class OrderReadService
    {
        private readonly ApplicationDbContext db;

        public OrderReadService(ApplicationDbContext db) { this.db = db; }

        public IList<OrderSummaryViewModel> ForSession(int sessionId)
        {
            return db.Orders.AsNoTracking().Where(order => order.RoomSessionId == sessionId)
                .Include(order => order.Items).Include(order => order.RoomSession.Room)
                .Include(order => order.RoomSession.Customer)
                .OrderByDescending(order => order.CreatedAt).ToList().Select(Map).ToList();
        }

        public OrderSummaryViewModel Find(int orderId)
        {
            var order = db.Orders.AsNoTracking().Include(item => item.Items)
                .Include(item => item.RoomSession.Room).Include(item => item.RoomSession.Customer)
                .SingleOrDefault(item => item.OrderId == orderId);
            return order == null ? null : Map(order);
        }

        private static OrderSummaryViewModel Map(Order order)
        {
            return new OrderSummaryViewModel
            {
                OrderId = order.OrderId,
                RoomSessionId = order.RoomSessionId,
                RoomName = order.RoomSession.Room.Name,
                CustomerName = order.RoomSession.Customer.FullName,
                Status = order.Status,
                CreatedAt = order.CreatedAt,
                Items = order.Items.Select(item => new OrderItemSummaryViewModel
                {
                    Name = item.ServiceNameSnapshot,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice
                }).ToList()
            };
        }
    }
}
