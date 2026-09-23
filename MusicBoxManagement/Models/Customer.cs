using System.ComponentModel.DataAnnotations;

namespace MusicBoxManagement.Models
{
    public class Customer
    {
        public int CustomerId { get; set; }

        [Required, StringLength(100)]
        public string FullName { get; set; }

        [Required, StringLength(10)]
        public string PhoneNumber { get; set; }
    }
}
