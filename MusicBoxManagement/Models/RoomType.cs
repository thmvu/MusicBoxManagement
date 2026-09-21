using System.ComponentModel.DataAnnotations;

namespace MusicBoxManagement.Models
{
    public class RoomType
    {
        public int RoomTypeId { get; set; }

        [Required, StringLength(20)]
        public string Code { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; }

        [Range(1, int.MaxValue)]
        public int Capacity { get; set; }

        [Range(typeof(decimal), "1", "9999999999999999", ErrorMessage = "Giá theo giờ phải là số nguyên đồng lớn hơn 0.")]
        public decimal PricePerHour { get; set; }

        [Required, StringLength(1000)]
        public string Amenities { get; set; }

        [StringLength(2000)]
        public string Description { get; set; }
    }
}
