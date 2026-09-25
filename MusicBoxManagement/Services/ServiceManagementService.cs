using System;
using System.Collections.Generic;
using System.Linq;
using MusicBoxManagement.Models;

namespace MusicBoxManagement.Services
{
    public static class ServiceCategories
    {
        public static readonly string[] All = { "Đồ uống", "Đồ ăn", "Khác" };
    }

    public sealed class ServiceManagementService
    {
        private readonly ApplicationDbContext db;

        public ServiceManagementService(ApplicationDbContext db)
        {
            this.db = db;
        }

        public IList<Models.Service> List()
        {
            return db.Services.OrderBy(item => item.Category).ThenBy(item => item.Name).ToList();
        }

        public ServiceFormViewModel GetForm(int id)
        {
            var item = db.Services.SingleOrDefault(service => service.ServiceId == id);
            if (item == null) return null;
            return new ServiceFormViewModel
            {
                ServiceId = item.ServiceId,
                Name = item.Name,
                Category = item.Category,
                Price = item.Price,
                Description = item.Description,
                IsActive = item.IsActive
            };
        }

        public int Create(ServiceFormViewModel model, string actorUserId)
        {
            Validate(model);
            var item = new Models.Service
            {
                Name = model.Name.Trim(),
                Category = model.Category,
                Price = model.Price,
                Description = Optional(model.Description),
                IsActive = model.IsActive
            };
            db.Services.Add(item);
            using (var transaction = db.Database.BeginTransaction())
            {
                db.SaveChanges();
                Log(actorUserId, "Create", item.ServiceId, "Tạo dịch vụ " + item.Name);
                db.SaveChanges();
                transaction.Commit();
            }
            return item.ServiceId;
        }

        public bool Update(ServiceFormViewModel model, string actorUserId)
        {
            Validate(model);
            var item = db.Services.SingleOrDefault(service => service.ServiceId == model.ServiceId);
            if (item == null) return false;
            item.Name = model.Name.Trim();
            item.Category = model.Category;
            item.Price = model.Price;
            item.Description = Optional(model.Description);
            item.IsActive = model.IsActive;
            Log(actorUserId, "Update", item.ServiceId, "Cập nhật dịch vụ " + item.Name);
            db.SaveChanges();
            return true;
        }

        private static void Validate(ServiceFormViewModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Name) || model.Name.Trim().Length > 100)
                throw new ArgumentException("Tên dịch vụ không hợp lệ.");
            if (!ServiceCategories.All.Contains(model.Category))
                throw new ArgumentException("Nhóm dịch vụ không hợp lệ.");
            if (model.Price <= 0 || model.Price != decimal.Truncate(model.Price))
                throw new ArgumentException("Giá phải là số nguyên đồng lớn hơn 0.");
            if (model.Description != null && model.Description.Length > 2000)
                throw new ArgumentException("Mô tả quá dài.");
        }

        private static string Optional(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private void Log(string actorUserId, string action, int serviceId, string description)
        {
            db.AuditLogs.Add(new AuditLog
            {
                ActorType = "Staff",
                UserId = actorUserId,
                Action = action,
                EntityName = "Service",
                EntityId = serviceId.ToString(),
                Description = description,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
    }
}
