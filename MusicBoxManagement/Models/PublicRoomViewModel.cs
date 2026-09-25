namespace MusicBoxManagement.Models
{
    public class PublicRoomViewModel
    {
        public int RoomId { get; set; }
        public string RoomCode { get; set; }
        public string Name { get; set; }
        public string ImageUrl { get; set; }
        public string Description { get; set; }
        public string RoomTypeName { get; set; }
        public int Capacity { get; set; }
        public decimal PricePerHour { get; set; }
        public string Amenities { get; set; }
    }
}
