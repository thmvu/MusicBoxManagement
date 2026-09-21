using System;
using System.Data.Entity;
using System.Linq;
using MusicBoxManagement.Models;

namespace MusicBoxManagement.Services
{
    public static class PermissionCodes
    {
        public static readonly string[] All = {
            "Dashboard.View", "Calendar.View", "Reservation.View", "Reservation.Create", "Reservation.Cancel",
            "Session.View", "Session.CheckIn", "Session.WalkIn", "Session.Extend", "Session.CheckOut",
            "Order.View", "Order.Create", "Order.Confirm", "Order.Cancel", "Invoice.View", "Invoice.Print",
            "Customer.View", "Customer.Create", "Customer.Edit", "Room.Manage", "RoomType.Edit",
            "Service.Manage", "Report.View", "Report.Export", "User.Manage", "Permission.Manage", "Audit.View"
        };

        public static readonly string[] StaffDefaults = All.TakeWhile(code => code != "Room.Manage").ToArray();
        public static readonly string[] ManagerDefaults = All.TakeWhile(code => code != "User.Manage").ToArray();
    }

    public sealed class PermissionService
    {
        private readonly ApplicationDbContext db;

        public PermissionService(ApplicationDbContext db)
        {
            this.db = db;
        }

        public bool HasPermission(string userId, string code)
        {
            if (string.IsNullOrEmpty(userId) || !PermissionCodes.All.Contains(code)) return false;
            var user = db.Users.Include(u => u.Roles).SingleOrDefault(u => u.Id == userId);
            if (user == null || !user.IsActive || user.Roles.Count != 1) return false;

            var roleId = user.Roles.Single().RoleId;
            var role = db.Roles.SingleOrDefault(r => r.Id == roleId);
            if (role == null) return false;
            if (role.Name == "Admin") return true;
            if (role.Name != "Staff" && role.Name != "Manager") return false;

            if (code == "User.Manage" || code == "Permission.Manage" || code == "Audit.View") return false;
            if (role.Name == "Staff" && (code == "Report.View" || code == "Report.Export")) return false;

            var granted = db.RolePermissions.Any(rp => rp.RoleId == roleId && rp.Permission.Code == code);
            if (!granted) return false;
            if (code == "Report.Export") return db.RolePermissions.Any(rp => rp.RoleId == roleId && rp.Permission.Code == "Report.View");
            if (code == "Invoice.Print") return db.RolePermissions.Any(rp => rp.RoleId == roleId && rp.Permission.Code == "Invoice.View");
            return true;
        }
    }
}
