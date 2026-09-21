using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Identity;
using MusicBoxManagement.Models;

namespace MusicBoxManagement.Services
{
    public sealed class UserManagementService
    {
        private static readonly string[] AllowedRoles = { "Staff", "Manager", "Admin" };
        private readonly ApplicationDbContext db;
        private readonly UserManager<ApplicationUser> userManager;

        public UserManagementService(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            this.db = db;
            this.userManager = userManager;
        }

        public IList<UserListItemViewModel> List()
        {
            var roleNames = db.Roles.ToDictionary(role => role.Id, role => role.Name);
            return db.Users.Include(user => user.Roles).OrderBy(user => user.UserName).ToList()
                .Select(user => new UserListItemViewModel
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    FullName = user.FullName,
                    IsActive = user.IsActive,
                    Role = string.Join(", ", user.Roles.Select(role => roleNames.ContainsKey(role.RoleId) ? roleNames[role.RoleId] : "?"))
                }).ToList();
        }

        public EditUserViewModel GetEdit(string id)
        {
            var user = db.Users.Include(item => item.Roles).SingleOrDefault(item => item.Id == id);
            if (user == null) return null;
            var roleIds = user.Roles.Select(userRole => userRole.RoleId).ToList();
            var roleId = roleIds.Count == 1 ? roleIds[0] : null;
            var role = roleId == null ? null : db.Roles.SingleOrDefault(item => item.Id == roleId);
            return new EditUserViewModel
            {
                Id = user.Id,
                UserName = user.UserName,
                FullName = user.FullName,
                IsActive = user.IsActive,
                Role = role == null ? null : role.Name
            };
        }

        public async Task<UserManagementResult> CreateAsync(CreateUserViewModel model, string actorId)
        {
            if (!AllowedRoles.Contains(model.Role)) return UserManagementResult.Failure("Vai trò không hợp lệ.");
            if (string.IsNullOrWhiteSpace(model.UserName) || string.IsNullOrWhiteSpace(model.FullName))
                return UserManagementResult.Failure("Tên đăng nhập và họ tên không được để trống.");

            using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                if (!IsActiveAdmin(actorId)) return UserManagementResult.Failure("Không có quyền quản lý tài khoản.");
                var user = new ApplicationUser
                {
                    UserName = model.UserName.Trim(),
                    FullName = model.FullName.Trim(),
                    IsActive = true
                };
                var created = await userManager.CreateAsync(user, model.Password);
                if (!created.Succeeded) return UserManagementResult.Failure(created.Errors.ToArray());
                var assigned = await userManager.AddToRoleAsync(user.Id, model.Role);
                if (!assigned.Succeeded) return UserManagementResult.Failure(assigned.Errors.ToArray());

                Log(actorId, "User.Create", user.Id, "Tạo tài khoản " + user.UserName + " với vai trò " + model.Role);
                await db.SaveChangesAsync();
                transaction.Commit();
                return UserManagementResult.Success(user.Id);
            }
        }

        public async Task<UserManagementResult> EditAsync(EditUserViewModel model, string actorId)
        {
            if (!AllowedRoles.Contains(model.Role)) return UserManagementResult.Failure("Vai trò không hợp lệ.");
            if (string.IsNullOrWhiteSpace(model.FullName)) return UserManagementResult.Failure("Họ tên không được để trống.");

            using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                if (!IsActiveAdmin(actorId)) return UserManagementResult.Failure("Không có quyền quản lý tài khoản.");
                var user = db.Users.Include(item => item.Roles).SingleOrDefault(item => item.Id == model.Id);
                if (user == null) return UserManagementResult.Failure("Không tìm thấy tài khoản.");

                var oldRoles = (await userManager.GetRolesAsync(user.Id)).ToArray();
                var wasAdmin = oldRoles.Contains("Admin");
                var removesAdmin = wasAdmin && (model.Role != "Admin" || !model.IsActive);
                if (user.Id == actorId && removesAdmin)
                    return UserManagementResult.Failure("Admin không thể tự khóa hoặc tự hạ vai trò.");

                if (removesAdmin)
                {
                    var adminRoleId = db.Roles.Where(role => role.Name == "Admin").Select(role => role.Id).Single();
                    var activeAdmins = db.Users.Count(item => item.IsActive && item.Roles.Any(role => role.RoleId == adminRoleId));
                    if (activeAdmins <= 1)
                        return UserManagementResult.Failure("Hệ thống phải còn ít nhất một Admin đang hoạt động.");
                }

                var roleChanged = oldRoles.Length != 1 || oldRoles[0] != model.Role;
                var activeChanged = user.IsActive != model.IsActive;
                user.FullName = model.FullName.Trim();
                user.IsActive = model.IsActive;
                var updated = await userManager.UpdateAsync(user);
                if (!updated.Succeeded) return UserManagementResult.Failure(updated.Errors.ToArray());

                if (roleChanged)
                {
                    if (oldRoles.Length > 0)
                    {
                        var removed = await userManager.RemoveFromRolesAsync(user.Id, oldRoles);
                        if (!removed.Succeeded) return UserManagementResult.Failure(removed.Errors.ToArray());
                    }
                    var assigned = await userManager.AddToRoleAsync(user.Id, model.Role);
                    if (!assigned.Succeeded) return UserManagementResult.Failure(assigned.Errors.ToArray());
                }

                if (roleChanged || activeChanged)
                {
                    var stamp = await userManager.UpdateSecurityStampAsync(user.Id);
                    if (!stamp.Succeeded) return UserManagementResult.Failure(stamp.Errors.ToArray());
                }

                Log(actorId, "User.Update", user.Id,
                    "Cập nhật tài khoản " + user.UserName + "; vai trò " + model.Role + "; hoạt động " + model.IsActive);
                await db.SaveChangesAsync();
                transaction.Commit();
                return UserManagementResult.Success(user.Id);
            }
        }

        public async Task<UserManagementResult> ResetPasswordAsync(string userId, string newPassword, string actorId)
        {
            using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                if (!IsActiveAdmin(actorId)) return UserManagementResult.Failure("Không có quyền quản lý tài khoản.");
                var user = await userManager.FindByIdAsync(userId);
                if (user == null) return UserManagementResult.Failure("Không tìm thấy tài khoản.");
                var token = await userManager.GeneratePasswordResetTokenAsync(user.Id);
                var reset = await userManager.ResetPasswordAsync(user.Id, token, newPassword);
                if (!reset.Succeeded) return UserManagementResult.Failure(reset.Errors.ToArray());

                Log(actorId, "User.ResetPassword", user.Id, "Đặt lại mật khẩu cho tài khoản " + user.UserName);
                await db.SaveChangesAsync();
                transaction.Commit();
                return UserManagementResult.Success(user.Id);
            }
        }

        private bool IsActiveAdmin(string actorId)
        {
            var actor = db.Users.Include(user => user.Roles).SingleOrDefault(user => user.Id == actorId);
            if (actor == null || !actor.IsActive || actor.Roles.Count != 1) return false;
            var roleId = actor.Roles.Single().RoleId;
            return db.Roles.Any(role => role.Id == roleId && role.Name == "Admin");
        }

        private void Log(string actorId, string action, string entityId, string description)
        {
            db.AuditLogs.Add(new AuditLog
            {
                ActorType = "Staff",
                UserId = actorId,
                Action = action,
                EntityName = "ApplicationUser",
                EntityId = entityId,
                Description = description,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
    }
}
