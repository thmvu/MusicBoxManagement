using System;
using System.ComponentModel.DataAnnotations;

namespace MusicBoxManagement.Models
{
    public static class ReservationStatuses
    {
        public const string Confirmed = "Confirmed";
        public const string CheckedIn = "CheckedIn";
        public const string Completed = "Completed";
        public const string Cancelled = "Cancelled";
        public const string NoShow = "NoShow";
    }

    public class Reservation
    {
        public int ReservationId { get; set; }

        public int CustomerId { get; set; }

        public int RoomId { get; set; }

        public DateTimeOffset StartTime { get; set; }

        public DateTimeOffset EndTime { get; set; }

        [Required, StringLength(20)]
        public string Status { get; set; }

        [StringLength(500)]
        public string CancellationReason { get; set; }

        [StringLength(128)]
        public string CreatedByUserId { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public virtual Customer Customer { get; set; }

        public virtual Room Room { get; set; }

        public virtual ApplicationUser CreatedByUser { get; set; }
    }
}
