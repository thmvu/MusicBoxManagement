using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Web;
using MusicBoxManagement.Models;

namespace MusicBoxManagement.Services
{
    public sealed class RoomService
    {
        private const int MaxImageBytes = 5 * 1024 * 1024;
        private readonly ApplicationDbContext db;
        private readonly string imageDirectory;

        public RoomService(ApplicationDbContext db, string imageDirectory)
        {
            this.db = db;
            this.imageDirectory = imageDirectory;
        }

        public IList<RoomListViewModel> List()
        {
            return db.Rooms.OrderBy(room => room.RoomCode)
                .Select(room => new RoomListViewModel
                {
                    RoomId = room.RoomId,
                    RoomCode = room.RoomCode,
                    Name = room.Name,
                    RoomTypeName = room.RoomType.Name,
                    ImageUrl = room.ImageUrl,
                    IsActive = room.IsActive,
                    InactiveReason = room.InactiveReason
                }).ToList();
        }

        public RoomFormViewModel GetForm(int id)
        {
            var room = db.Rooms.SingleOrDefault(item => item.RoomId == id);
            if (room == null) return null;
            return new RoomFormViewModel
            {
                RoomId = room.RoomId,
                RoomCode = room.RoomCode,
                RoomTypeId = room.RoomTypeId,
                Name = room.Name,
                Description = room.Description,
                ImageUrl = room.ImageUrl
            };
        }

        public bool RoomCodeExists(string code, int exceptRoomId = 0)
        {
            var normalized = (code ?? "").Trim();
            return db.Rooms.Any(room => room.RoomCode == normalized && room.RoomId != exceptRoomId);
        }

        public bool RoomTypeExists(int id)
        {
            return db.RoomTypes.Any(roomType => roomType.RoomTypeId == id);
        }

        public int Create(RoomFormViewModel model, string actorUserId)
        {
            ValidateFields(model, true);
            if (!RoomTypeExists(model.RoomTypeId)) throw new ArgumentException("Loại phòng không tồn tại.");
            if (RoomCodeExists(model.RoomCode)) throw new ArgumentException("Mã phòng đã tồn tại.");

            var image = PrepareImage(model.Image);
            var room = new Room
            {
                RoomCode = model.RoomCode.Trim(),
                RoomTypeId = model.RoomTypeId,
                Name = model.Name.Trim(),
                Description = NormalizeOptional(model.Description),
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            var filePath = SaveImage(image);
            try
            {
                room.ImageUrl = ImageUrl(filePath);
                db.Rooms.Add(room);
                db.AuditLogs.Add(NewAudit(actorUserId, "Create", room, "Tạo phòng " + room.RoomCode));
                db.SaveChanges();
                return room.RoomId;
            }
            catch
            {
                DeleteFile(filePath);
                throw;
            }
        }

        public bool Update(RoomFormViewModel model, string actorUserId)
        {
            ValidateFields(model, false);
            var room = db.Rooms.SingleOrDefault(item => item.RoomId == model.RoomId);
            if (room == null) return false;
            if (!RoomTypeExists(model.RoomTypeId)) throw new ArgumentException("Loại phòng không tồn tại.");

            // RoomType change must also check Active Session once RoomSession exists.
            // No session can exist in this milestone; wire this check into the session milestone.
            var image = model.Image == null || model.Image.ContentLength == 0 ? null : PrepareImage(model.Image);
            var newFilePath = image == null ? null : SaveImage(image);
            var oldImageUrl = room.ImageUrl;
            try
            {
                room.RoomTypeId = model.RoomTypeId;
                room.Name = model.Name.Trim();
                room.Description = NormalizeOptional(model.Description);
                if (newFilePath != null) room.ImageUrl = ImageUrl(newFilePath);
                db.AuditLogs.Add(NewAudit(actorUserId, "Update", room, "Cập nhật phòng " + room.RoomCode));
                db.SaveChanges();
            }
            catch
            {
                DeleteFile(newFilePath);
                throw;
            }

            if (newFilePath != null && !string.IsNullOrEmpty(oldImageUrl))
            {
                var oldFileName = Path.GetFileName(oldImageUrl);
                if (Guid.TryParseExact(Path.GetFileNameWithoutExtension(oldFileName), "N", out _))
                    DeleteFile(Path.Combine(imageDirectory, oldFileName));
            }
            return true;
        }

        private static void ValidateFields(RoomFormViewModel model, bool creating)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Name) ||
                (creating && string.IsNullOrWhiteSpace(model.RoomCode)))
                throw new ArgumentException("Mã và tên phòng là bắt buộc.");
            if (creating && (model.Image == null || model.Image.ContentLength == 0))
                throw new ArgumentException("Cần chọn một ảnh phòng.");
        }

        private static string NormalizeOptional(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static AuditLog NewAudit(string actorUserId, string action, Room room, string description)
        {
            return new AuditLog
            {
                ActorType = "Staff",
                UserId = actorUserId,
                Action = action,
                EntityName = "Room",
                EntityId = room.RoomCode,
                Description = description,
                CreatedAt = DateTimeOffset.UtcNow
            };
        }

        private static ImageFile PrepareImage(HttpPostedFileBase upload)
        {
            if (upload == null || upload.ContentLength <= 0 || upload.ContentLength > MaxImageBytes)
                throw new ArgumentException("Ảnh phải có dung lượng tối đa 5 MB.");

            using (var buffer = new MemoryStream())
            {
                var chunk = new byte[8192];
                int read;
                while ((read = upload.InputStream.Read(chunk, 0, chunk.Length)) > 0)
                {
                    if (buffer.Length + read > MaxImageBytes)
                        throw new ArgumentException("Ảnh phải có dung lượng tối đa 5 MB.");
                    buffer.Write(chunk, 0, read);
                }
                var bytes = buffer.ToArray();
                var extension = DetectImageExtension(bytes);
                if (extension == null) throw new ArgumentException("Chỉ nhận ảnh JPEG, PNG hoặc WebP hợp lệ.");
                return new ImageFile { Bytes = bytes, Extension = extension };
            }
        }

        private static string DetectImageExtension(byte[] bytes)
        {
            if (bytes.Length >= 4 && bytes[0] == 0xff && bytes[1] == 0xd8 &&
                bytes[bytes.Length - 2] == 0xff && bytes[bytes.Length - 1] == 0xd9 &&
                CanDecode(bytes, ImageFormat.Jpeg)) return ".jpg";

            if (bytes.Length >= 12 && bytes[0] == 0x89 && bytes[1] == 0x50 &&
                bytes[2] == 0x4e && bytes[3] == 0x47 && CanDecode(bytes, ImageFormat.Png)) return ".png";

            if (bytes.Length >= 20 && bytes[0] == 'R' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == 'F' &&
                bytes[8] == 'W' && bytes[9] == 'E' && bytes[10] == 'B' && bytes[11] == 'P' &&
                BitConverter.ToUInt32(bytes, 4) == bytes.Length - 8 &&
                bytes[12] == 'V' && bytes[13] == 'P' && bytes[14] == '8' &&
                (bytes[15] == ' ' || bytes[15] == 'L' || bytes[15] == 'X') &&
                BitConverter.ToUInt32(bytes, 16) <= bytes.Length - 20) return ".webp";
            return null;
        }

        private static bool CanDecode(byte[] bytes, ImageFormat format)
        {
            try
            {
                using (var stream = new MemoryStream(bytes))
                using (var image = Image.FromStream(stream, false, true))
                    return image.RawFormat.Guid == format.Guid && image.Width > 0 && image.Height > 0;
            }
            catch (ArgumentException) { return false; }
            catch (OutOfMemoryException) { return false; }
        }

        private string SaveImage(ImageFile image)
        {
            Directory.CreateDirectory(imageDirectory);
            var path = Path.Combine(imageDirectory, Guid.NewGuid().ToString("N") + image.Extension);
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
                file.Write(image.Bytes, 0, image.Bytes.Length);
            return path;
        }

        private static string ImageUrl(string filePath)
        {
            return "~/Content/uploads/rooms/" + Path.GetFileName(filePath);
        }

        private static void DeleteFile(string path)
        {
            if (path == null) return;
            try { File.Delete(path); } catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private sealed class ImageFile
        {
            public byte[] Bytes { get; set; }
            public string Extension { get; set; }
        }
    }
}
