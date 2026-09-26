using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MusicBoxManagement.Models
{
    public class GuestBookingFormViewModel
    {
        [Range(1, int.MaxValue)]
        public int RoomId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày đặt.")]
        public string BookingDate { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn giờ bắt đầu.")]
        public string StartSlot { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn thời lượng.")]
        public int? DurationMinutes { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
        [StringLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự.")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
        public string PhoneNumber { get; set; }
    }

    public class PublicRoomDetailsViewModel
    {
        public PublicRoomViewModel Room { get; set; }

        public GuestBookingFormViewModel Booking { get; set; }

        public string ScheduleDate { get; set; }

        public IList<GuestScheduleSlot> ScheduleSlots { get; set; }
    }
}
