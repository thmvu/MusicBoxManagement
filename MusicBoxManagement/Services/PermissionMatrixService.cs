using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using MusicBoxManagement.Models;

namespace MusicBoxManagement.Services
{
    public sealed class PermissionMutationResult
    {
        public bool Succeeded { get; private set; }
        public string Error { get; private set; }
        public static PermissionMutationResult Success() { return new PermissionMutationResult { Succeeded = true }; }
        public static PermissionMutationResult Failure(string error) { return new PermissionMutationResult { Error = error }; }
    }

    public sealed class PermissionMatrixService
    {
        private readonly ApplicationDbContext db;

        public PermissionMatrixService(ApplicationDbContext db)
        {
            this.db = db;
        }

        public static bool CanEdit(string role, string code)
        {
            return (role == "Staff" || role == "Manager") &&
                PermissionCodes.All.Contains(code) &&
                code != "User.Manage" && code != "Permission.Manage" && code != "Audit.View" &&
                (role != "Staff" || (code != "Report.View" && code != "Report.Export"));
        }

        public IList<PermissionMatrixRowViewModel> GetRows()
        {
            var roles = db.Roles.AsNoTracking()
                .Where(item => item.Name == "Staff" || item.Name == "Manager")
                .ToDictionary(item => item.Name, item => item.Id);
            var ids = roles.Values.ToList();
            var grants = db.RolePermissions.AsNoTracking()
                .Where(item => ids.Contains(item.RoleId))
                .Select(item => new { item.RoleId, item.Permission.Code }).ToList();
            return PermissionCodes.All.Select(code => new PermissionMatrixRowViewModel
            {
                Code = code,
                StaffGranted = roles.ContainsKey("Staff") && grants.Any(item =>
                    item.RoleId == roles["Staff"] && item.Code == code),
                ManagerGranted = roles.ContainsKey("Manager") && grants.Any(item =>
                    item.RoleId == roles["Manager"] && item.Code == code),
                StaffEditable = CanEdit("Staff", code),
                ManagerEditable = CanEdit("Manager", code)
            }).ToList();
        }

        public PermissionMutationResult SetGrant(string role, string code, bool enabled, string actorUserId)
        {
            if (!CanEdit(role, code))
                return PermissionMutationResult.Failure("Quyền này không được chỉnh cho vai trò đã chọn.");
            if (string.IsNullOrWhiteSpace(actorUserId))
                return PermissionMutationResult.Failure("Thiếu người thực hiện.");
            try
            {
                using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
                {
                    var actor = db.Users.SingleOrDefault(item => item.Id == actorUserId && item.IsActive);
                    if (actor == null || !new PermissionService(db)
                        .HasPermission(actorUserId, "Permission.Manage"))
                        return PermissionMutationResult.Failure("Chỉ Admin được chỉnh quyền.");
                    var roleId = db.Roles.Where(item => item.Name == role).Select(item => item.Id).SingleOrDefault();
                    var permissionId = db.Permissions.Where(item => item.Code == code)
                        .Select(item => item.PermissionId).SingleOrDefault();
                    if (roleId == null || permissionId == 0)
                        return PermissionMutationResult.Failure("Vai trò hoặc quyền chưa được khởi tạo.");
                    var current = db.RolePermissions.SingleOrDefault(item =>
                        item.RoleId == roleId && item.PermissionId == permissionId);
                    if ((current != null) == enabled) return PermissionMutationResult.Success();
                    if (enabled)
                        db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permissionId });
                    else
                        db.RolePermissions.Remove(current);
                    db.AuditLogs.Add(new AuditLog
                    {
                        ActorType = "Staff", UserId = actorUserId,
                        Action = enabled ? "GrantPermission" : "RevokePermission",
                        EntityName = "RolePermission", EntityId = role + ":" + code,
                        Description = (enabled ? "Đã cấp " : "Đã thu hồi ") + code + " cho " + role + ".",
                        CreatedAt = DateTimeOffset.UtcNow
                    });
                    db.SaveChanges();
                    transaction.Commit();
                    return PermissionMutationResult.Success();
                }
            }
            catch (Exception error)
            {
                for (var current = error; current != null; current = current.InnerException)
                {
                    var sql = current as SqlException;
                    if (sql != null && (sql.Number == 1205 || sql.Number == 2601 || sql.Number == 2627))
                        return PermissionMutationResult.Failure("Quyền vừa thay đổi. Vui lòng tải lại và thử lại.");
                }
                throw;
            }
        }
    }
}
