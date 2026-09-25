using System.ComponentModel.DataAnnotations;

namespace MusicBoxManagement.Models
{
    public class ServiceFormViewModel
    {
        public int ServiceId { get; set; }

        [Required(ErrorMessage = "Tên dịch vụ là bắt buộc."), StringLength(100)]
        [Display(Name = "Tên dịch vụ")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Chọn nhóm dịch vụ.")]
        [Display(Name = "Nhóm")]
        public string Category { get; set; }

        [Range(typeof(decimal), "1", "9999999999999999", ErrorMessage = "Giá phải lớn hơn 0.")]
        [Display(Name = "Giá (đồng)")]
        public decimal Price { get; set; }

        [StringLength(2000)]
        [Display(Name = "Mô tả")]
        public string Description { get; set; }

        [Display(Name = "Đang bán")]
        public bool IsActive { get; set; }
    }
}
