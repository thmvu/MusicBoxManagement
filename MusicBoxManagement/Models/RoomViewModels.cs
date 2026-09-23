using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web;

namespace MusicBoxManagement.Models
{
    public class RoomFormViewModel
    {
        public int RoomId { get; set; }

        [Required(ErrorMessage = "Mã phòng là bắt buộc."), StringLength(30)]
        [Display(Name = "Mã phòng")]
        public string RoomCode { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Chọn loại phòng.")]
        [Display(Name = "Loại phòng")]
        public int RoomTypeId { get; set; }

        [Required(ErrorMessage = "Tên phòng là bắt buộc."), StringLength(100)]
        [Display(Name = "Tên phòng")]
        public string Name { get; set; }

        [StringLength(2000)]
        [Display(Name = "Mô tả")]
        public string Description { get; set; }

        [Display(Name = "Ảnh phòng")]
        public HttpPostedFileBase Image { get; set; }

        public string ImageUrl { get; set; }
    }

    public class RoomListViewModel
    {
        public int RoomId { get; set; }
        public string RoomCode { get; set; }
        public string Name { get; set; }
        public string RoomTypeName { get; set; }
        public string ImageUrl { get; set; }
        public bool IsActive { get; set; }
        public string InactiveReason { get; set; }
    }
}
