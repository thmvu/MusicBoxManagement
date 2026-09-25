using System;
using System.ComponentModel.DataAnnotations;

namespace MusicBoxManagement.Models
{
    public static class RoomSessionStatuses
    {
        public const string Active = "Active";
        public const string Completed = "Completed";
    }

    public class RoomSession
    {
        public int RoomSessionId { get; set; }

        public int CustomerId { get; set; }

        public int RoomId { get; set; }

        public int? ReservationId { get; set; }

        public DateTimeOffset ActualStartTime { get; set; }

        public DateTimeOffset? ExpectedEndTime { get; set; }

        public DateTimeOffset? ActualEndTime { get; set; }

        public decimal HourlyRate { get; set; }

        [Required, StringLength(30)]
        public string RoomCodeSnapshot { get; set; }

        [Required, StringLength(20)]
        public string RoomTypeCodeSnapshot { get; set; }

        [Required, StringLength(100)]
        public string RoomTypeNameSnapshot { get; set; }

        [Required, StringLength(20)]
        public string Status { get; set; }

        public virtual Customer Customer { get; set; }

        public virtual Room Room { get; set; }

        public virtual Reservation Reservation { get; set; }
    }
}
