using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using MusicBoxManagement.Models;

namespace MusicBoxManagement.Services
{
    public sealed class PublicRoomCatalogService
    {
        private readonly ApplicationDbContext db;

        public PublicRoomCatalogService(ApplicationDbContext db)
        {
            this.db = db;
        }

        public IList<PublicRoomViewModel> List()
        {
            return ActiveRooms().OrderBy(room => room.RoomCode).ToList();
        }

        public PublicRoomViewModel Get(int id)
        {
            return ActiveRooms().SingleOrDefault(room => room.RoomId == id);
        }

        private IQueryable<PublicRoomViewModel> ActiveRooms()
        {
            return db.Rooms.AsNoTracking()
                .Where(room => room.IsActive)
                .Select(room => new PublicRoomViewModel
                {
                    RoomId = room.RoomId,
                    RoomCode = room.RoomCode,
                    Name = room.Name,
                    ImageUrl = room.ImageUrl,
                    Description = room.Description,
                    RoomTypeName = room.RoomType.Name,
                    Capacity = room.RoomType.Capacity,
                    PricePerHour = room.RoomType.PricePerHour,
                    Amenities = room.RoomType.Amenities
                });
        }
    }
}
