using System;
using System.Collections.Generic;

namespace MusicBoxManagement.Models
{
    public sealed class DashboardViewModel
    {
        public DateTimeOffset UpdatedAt { get; set; }
        public int TotalRooms { get; set; }
        public int OccupiedRooms { get; set; }
        public int TodayReservations { get; set; }
        public int ActiveSessions { get; set; }
        public int PendingOrders { get; set; }
        public IList<DashboardAlertViewModel> Alerts { get; set; } = new List<DashboardAlertViewModel>();
        public IList<DashboardSessionViewModel> Sessions { get; set; } = new List<DashboardSessionViewModel>();
    }

    public sealed class DashboardAlertViewModel
    {
        public string Title { get; set; }
        public string Detail { get; set; }
        public string ActionText { get; set; }
        public string ActionUrl { get; set; }
    }

    public sealed class DashboardSessionViewModel
    {
        public int RoomSessionId { get; set; }
        public int? ReservationId { get; set; }
        public string RoomName { get; set; }
        public string CustomerName { get; set; }
        public DateTimeOffset ActualStartTime { get; set; }
        public DateTimeOffset? ExpectedEndTime { get; set; }
    }
}
