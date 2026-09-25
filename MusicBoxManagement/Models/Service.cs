using System.ComponentModel.DataAnnotations;

namespace MusicBoxManagement.Models
{
    public class Service
    {
        public int ServiceId { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; }

        [Required, StringLength(20)]
        public string Category { get; set; }

        public decimal Price { get; set; }

        [StringLength(2000)]
        public string Description { get; set; }

        public bool IsActive { get; set; }
    }
}
