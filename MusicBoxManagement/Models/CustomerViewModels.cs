using System.ComponentModel.DataAnnotations;

namespace MusicBoxManagement.Models
{
    public class CustomerFormViewModel
    {
        public int CustomerId { get; set; }

        [Required(ErrorMessage = "Họ tên là bắt buộc."), StringLength(100)]
        [Display(Name = "Họ tên")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Số điện thoại là bắt buộc.")]
        [Display(Name = "Số điện thoại")]
        public string PhoneNumber { get; set; }
    }
}
