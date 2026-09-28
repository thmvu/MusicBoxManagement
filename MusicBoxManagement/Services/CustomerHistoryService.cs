using System.Data.Entity;
using System.Linq;
using MusicBoxManagement.Models;

namespace MusicBoxManagement.Services
{
    public sealed class CustomerHistoryService
    {
        private readonly ApplicationDbContext db;

        public CustomerHistoryService(ApplicationDbContext db)
        {
            this.db = db;
        }

        public CustomerDetailsViewModel GetDetails(int customerId, bool canViewReservations,
            bool canViewSessions, bool canViewInvoices)
        {
            var customer = db.Customers.AsNoTracking()
                .Where(item => item.CustomerId == customerId)
                .Select(item => new { item.CustomerId, item.FullName, item.PhoneNumber })
                .SingleOrDefault();
            if (customer == null) return null;

            var model = new CustomerDetailsViewModel
            {
                CustomerId = customer.CustomerId,
                FullName = customer.FullName,
                PhoneNumber = customer.PhoneNumber,
                CanViewReservations = canViewReservations,
                CanViewSessions = canViewSessions,
                CanViewInvoices = canViewInvoices,
                CompletedSessionCount = db.RoomSessions.Count(item =>
                    item.CustomerId == customerId && item.Status == RoomSessionStatuses.Completed),
                LastUsedAt = db.RoomSessions.Where(item => item.CustomerId == customerId)
                    .Select(item => (System.DateTimeOffset?)item.ActualStartTime)
                    .Max()
            };
            if (canViewReservations)
                model.Reservations = db.Reservations.AsNoTracking()
                    .Where(item => item.CustomerId == customerId)
                    .OrderByDescending(item => item.StartTime)
                    .Select(item => new CustomerReservationHistoryItem
                    {
                        ReservationId = item.ReservationId,
                        RoomCode = item.Room.RoomCode,
                        StartTime = item.StartTime,
                        EndTime = item.EndTime,
                        Status = item.Status
                    }).ToList();
            if (canViewSessions)
                model.Sessions = db.RoomSessions.AsNoTracking()
                    .Where(item => item.CustomerId == customerId)
                    .OrderByDescending(item => item.ActualStartTime)
                    .Select(item => new CustomerSessionHistoryItem
                    {
                        RoomSessionId = item.RoomSessionId,
                        RoomCode = item.RoomCodeSnapshot,
                        ActualStartTime = item.ActualStartTime,
                        ActualEndTime = item.ActualEndTime,
                        Status = item.Status
                    }).ToList();
            if (canViewInvoices)
                model.Invoices = db.Invoices.AsNoTracking()
                    .Where(item => item.RoomSession.CustomerId == customerId)
                    .OrderByDescending(item => item.PaidAt)
                    .Select(item => new CustomerInvoiceHistoryItem
                    {
                        InvoiceId = item.InvoiceId,
                        InvoiceNumber = item.InvoiceNumber,
                        RoomCode = item.RoomSession.RoomCodeSnapshot,
                        PaidAt = item.PaidAt,
                        TotalAmount = item.TotalAmount
                    }).ToList();
            return model;
        }
    }
}
