using System.ComponentModel.DataAnnotations;

namespace MusicBoxManagement.Models
{
    public class EditRoomTypeViewModel
    {
        public int RoomTypeId { get; set; }

        public string Code { get; set; }

        [Required(ErrorMessage = "Tên loại phòng là bắt buộc."), StringLength(100)]
        [Display(Name = "Tên loại phòng")]
        public string Name { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Sức chứa phải lớn hơn 0.")]
        [Display(Name = "Sức chứa")]
        public int Capacity { get; set; }

        [Range(typeof(decimal), "1", "9999999999999999", ErrorMessage = "Giá theo giờ phải là số nguyên đồng lớn hơn 0.")]
        [Display(Name = "Giá theo giờ (đồng)")]
        public decimal PricePerHour { get; set; }

        [Required(ErrorMessage = "Tiện ích là bắt buộc."), StringLength(1000)]
        [Display(Name = "Tiện ích")]
        public string Amenities { get; set; }

        [StringLength(2000)]
        [Display(Name = "Mô tả")]
        public string Description { get; set; }
    }
}
