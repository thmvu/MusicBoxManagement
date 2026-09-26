using System;
using System.Collections.Generic;

namespace MusicBoxManagement.Models
{
    public sealed class ReservationListViewModel
    {
        public string Date { get; set; }
        public IList<ReservationListItemViewModel> Items { get; set; }
    }

    public sealed class ReservationListItemViewModel
    {
        public int ReservationId { get; set; }
        public string RoomName { get; set; }
        public string CustomerName { get; set; }
        public string PhoneNumber { get; set; }
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }
        public string Status { get; set; }
    }

    public sealed class ReservationDetailsViewModel
    {
        public int ReservationId { get; set; }
        public string RoomName { get; set; }
        public string CustomerName { get; set; }
        public string PhoneNumber { get; set; }
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }
        public string Status { get; set; }
        public string CancellationReason { get; set; }
        public bool CanCancel { get; set; }
    }
}
