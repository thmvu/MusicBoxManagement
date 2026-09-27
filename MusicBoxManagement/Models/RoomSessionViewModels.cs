using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MusicBoxManagement.Models
{
    public sealed class WalkInFormViewModel
    {
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn phòng.")]
        public int RoomId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
        [StringLength(100)]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
        public string PhoneNumber { get; set; }
    }

    public sealed class RoomSessionListItemViewModel
    {
        public int RoomSessionId { get; set; }
        public string RoomName { get; set; }
        public string CustomerName { get; set; }
        public DateTimeOffset ActualStartTime { get; set; }
        public bool FromReservation { get; set; }
    }

    public sealed class RoomSessionDetailsViewModel
    {
        public int RoomSessionId { get; set; }
        public string RoomName { get; set; }
        public string CustomerName { get; set; }
        public string PhoneNumber { get; set; }
        public int? ReservationId { get; set; }
        public string Status { get; set; }
        public DateTimeOffset ActualStartTime { get; set; }
        public DateTimeOffset? ExpectedEndTime { get; set; }
        public DateTimeOffset? WarningEndUtc { get; set; }
        public decimal HourlyRate { get; set; }
        public string RoomCodeSnapshot { get; set; }
        public string RoomTypeNameSnapshot { get; set; }
        public bool CanExtend { get; set; }
    }
}
