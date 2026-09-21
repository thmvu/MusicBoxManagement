using System;
using System.Collections.Generic;
using System.Linq;
using MusicBoxManagement.Models;

namespace MusicBoxManagement.Services
{
    public sealed class RoomTypeService
    {
        private readonly ApplicationDbContext db;

        public RoomTypeService(ApplicationDbContext db)
        {
            this.db = db;
        }

        public IList<RoomType> List()
        {
            return db.RoomTypes.OrderBy(roomType => roomType.Code).ToList();
        }

        public EditRoomTypeViewModel GetEdit(int id)
        {
            var roomType = db.RoomTypes.SingleOrDefault(item => item.RoomTypeId == id);
            if (roomType == null) return null;
            return new EditRoomTypeViewModel
            {
                RoomTypeId = roomType.RoomTypeId,
                Code = roomType.Code,
                Name = roomType.Name,
                Capacity = roomType.Capacity,
                PricePerHour = roomType.PricePerHour,
                Amenities = roomType.Amenities,
                Description = roomType.Description
            };
        }

        public bool Update(EditRoomTypeViewModel model, string actorUserId)
        {
            var roomType = db.RoomTypes.SingleOrDefault(item => item.RoomTypeId == model.RoomTypeId);
            if (roomType == null || (roomType.Code != "STANDARD" && roomType.Code != "VIP")) return false;
            if (string.IsNullOrWhiteSpace(model.Name) || string.IsNullOrWhiteSpace(model.Amenities) ||
                model.Capacity <= 0 || model.PricePerHour <= 0 || model.PricePerHour != decimal.Truncate(model.PricePerHour))
                throw new ArgumentException("Dữ liệu loại phòng không hợp lệ.", nameof(model));

            roomType.Name = model.Name.Trim();
            roomType.Capacity = model.Capacity;
            roomType.PricePerHour = model.PricePerHour;
            roomType.Amenities = model.Amenities.Trim();
            roomType.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
            db.AuditLogs.Add(new AuditLog
            {
                ActorType = "Staff",
                UserId = actorUserId,
                Action = "Update",
                EntityName = "RoomType",
                EntityId = roomType.RoomTypeId.ToString(),
                Description = "Cập nhật loại phòng " + roomType.Code,
                CreatedAt = DateTimeOffset.UtcNow
            });
            db.SaveChanges();
            return true;
        }
    }
}
