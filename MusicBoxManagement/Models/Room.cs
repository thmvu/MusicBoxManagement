using System;
using System.ComponentModel.DataAnnotations;

namespace MusicBoxManagement.Models
{
    public class Room
    {
        public int RoomId { get; set; }

        [Required, StringLength(30)]
        public string RoomCode { get; set; }

        public int RoomTypeId { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; }

        [StringLength(300)]
        public string ImageUrl { get; set; }

        [StringLength(2000)]
        public string Description { get; set; }

        public bool IsActive { get; set; }

        [StringLength(500)]
        public string InactiveReason { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public virtual RoomType RoomType { get; set; }
    }
}
