namespace MusicBoxManagement.Migrations
{
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Migrations;
    using System.Linq;
    using Microsoft.AspNet.Identity;
    using Microsoft.AspNet.Identity.EntityFramework;
    using MusicBoxManagement.Models;
    using MusicBoxManagement.Services;

    internal sealed class Configuration : DbMigrationsConfiguration<MusicBoxManagement.Models.ApplicationDbContext>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = false;
            ContextKey = "MusicBoxManagement.Models.ApplicationDbContext";
        }

        protected override void Seed(MusicBoxManagement.Models.ApplicationDbContext context)
        {
            SeedRoomType(context, "STANDARD", "Standard", 4, 120000, "TV, Điều hòa, 2 micro, Loa, Đèn LED");
            SeedRoomType(context, "VIP", "VIP", 6, 200000, "TV lớn, Điều hòa, 4 micro, Loa cao cấp, Đèn LED, Sofa");
            context.SaveChanges();

            var roles = new RoleManager<IdentityRole>(new RoleStore<IdentityRole>(context));
            var newRoles = new System.Collections.Generic.HashSet<string>();
            foreach (var name in new[] { "Staff", "Manager", "Admin" })
                if (!roles.RoleExists(name))
                {
                    Check(roles.Create(new IdentityRole(name)));
                    newRoles.Add(name);
                }

            foreach (var code in PermissionCodes.All)
                if (!context.Permissions.Any(p => p.Code == code))
                    context.Permissions.Add(new Permission { Code = code, Name = code });
            context.SaveChanges();

            if (newRoles.Contains("Staff")) SeedDefaults(context, roles.FindByName("Staff").Id, PermissionCodes.StaffDefaults);
            if (newRoles.Contains("Manager")) SeedDefaults(context, roles.FindByName("Manager").Id, PermissionCodes.ManagerDefaults);

            // Local bootstrap only: supply both environment variables when running Update-Database.
            // Existing users are never promoted implicitly.
            var adminName = Environment.GetEnvironmentVariable("MUSICBOX_ADMIN_USERNAME");
            var adminPassword = Environment.GetEnvironmentVariable("MUSICBOX_ADMIN_PASSWORD");
            if (!string.IsNullOrWhiteSpace(adminName) && !string.IsNullOrWhiteSpace(adminPassword))
            {
                var manager = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(context));
                manager.PasswordValidator = new PasswordValidator { RequiredLength = 12, RequireDigit = true, RequireLowercase = true, RequireUppercase = true, RequireNonLetterOrDigit = true };
                var user = manager.FindByName(adminName);
                if (user == null)
                {
                    user = new ApplicationUser { UserName = adminName, FullName = "Administrator", IsActive = true };
                    Check(manager.Create(user, adminPassword));
                    Check(manager.AddToRole(user.Id, "Admin"));
                }
                else if (!manager.IsInRole(user.Id, "Admin"))
                {
                    throw new InvalidOperationException("Bootstrap username already exists without Admin role.");
                }
            }
        }

        private static void SeedRoomType(ApplicationDbContext context, string code, string name, int capacity, decimal pricePerHour, string amenities)
        {
            if (!context.RoomTypes.Any(roomType => roomType.Code == code))
                context.RoomTypes.Add(new RoomType { Code = code, Name = name, Capacity = capacity, PricePerHour = pricePerHour, Amenities = amenities });
        }

        private static void SeedDefaults(ApplicationDbContext context, string roleId, string[] codes)
        {
            foreach (var code in codes)
            {
                var permissionId = context.Permissions.Where(p => p.Code == code).Select(p => p.PermissionId).Single();
                if (!context.RolePermissions.Any(rp => rp.RoleId == roleId && rp.PermissionId == permissionId))
                    context.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permissionId });
            }
            context.SaveChanges();
        }

        private static void Check(IdentityResult result)
        {
            if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors));
        }
    }
}
