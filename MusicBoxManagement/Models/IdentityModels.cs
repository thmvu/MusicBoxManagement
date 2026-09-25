using System.Data.Entity;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;

namespace MusicBoxManagement.Models
{
    // You can add profile data for the user by adding more properties to your ApplicationUser class, please visit https://go.microsoft.com/fwlink/?LinkID=317594 to learn more.
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [StringLength(100)]
        public string FullName { get; set; }

        public bool IsActive { get; set; } = true;

        public async Task<ClaimsIdentity> GenerateUserIdentityAsync(UserManager<ApplicationUser> manager)
        {
            // Note the authenticationType must match the one defined in CookieAuthenticationOptions.AuthenticationType
            var userIdentity = await manager.CreateIdentityAsync(this, DefaultAuthenticationTypes.ApplicationCookie);
            // Add custom user claims here
            return userIdentity;
        }
    }

    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public DbSet<Permission> Permissions { get; set; }

        public DbSet<RolePermission> RolePermissions { get; set; }

        public DbSet<AuditLog> AuditLogs { get; set; }

        public DbSet<RoomType> RoomTypes { get; set; }

        public DbSet<Room> Rooms { get; set; }

        public DbSet<Customer> Customers { get; set; }

        public DbSet<Service> Services { get; set; }

        public DbSet<Reservation> Reservations { get; set; }

        public DbSet<RoomSession> RoomSessions { get; set; }

        public ApplicationDbContext()
            : base("DefaultConnection", throwIfV1Schema: false)
        {
        }

        public static ApplicationDbContext Create()
        {
            return new ApplicationDbContext();
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Permission>()
                .Property(permission => permission.Code)
                .IsRequired()
                .HasMaxLength(128)
                .HasColumnAnnotation("Index", new System.Data.Entity.Infrastructure.Annotations.IndexAnnotation(
                    new System.ComponentModel.DataAnnotations.Schema.IndexAttribute("IX_Permission_Code") { IsUnique = true }));

            modelBuilder.Entity<RolePermission>()
                .HasRequired(rolePermission => rolePermission.Role)
                .WithMany()
                .HasForeignKey(rolePermission => rolePermission.RoleId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<RolePermission>()
                .HasRequired(rolePermission => rolePermission.Permission)
                .WithMany()
                .HasForeignKey(rolePermission => rolePermission.PermissionId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<AuditLog>()
                .HasOptional(auditLog => auditLog.User)
                .WithMany()
                .HasForeignKey(auditLog => auditLog.UserId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<RoomType>()
                .Property(roomType => roomType.Code)
                .IsRequired()
                .HasMaxLength(20)
                .HasColumnAnnotation("Index", new System.Data.Entity.Infrastructure.Annotations.IndexAnnotation(
                    new System.ComponentModel.DataAnnotations.Schema.IndexAttribute("IX_RoomType_Code") { IsUnique = true }));

            modelBuilder.Entity<RoomType>()
                .Property(roomType => roomType.PricePerHour)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Room>()
                .Property(room => room.RoomCode)
                .IsRequired()
                .HasMaxLength(30)
                .HasColumnAnnotation("Index", new System.Data.Entity.Infrastructure.Annotations.IndexAnnotation(
                    new System.ComponentModel.DataAnnotations.Schema.IndexAttribute("IX_Room_RoomCode") { IsUnique = true }));

            modelBuilder.Entity<Room>()
                .HasRequired(room => room.RoomType)
                .WithMany()
                .HasForeignKey(room => room.RoomTypeId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Customer>()
                .Property(customer => customer.PhoneNumber)
                .IsRequired()
                .HasMaxLength(10)
                .HasColumnAnnotation("Index", new System.Data.Entity.Infrastructure.Annotations.IndexAnnotation(
                    new System.ComponentModel.DataAnnotations.Schema.IndexAttribute("IX_Customer_PhoneNumber") { IsUnique = true }));

            modelBuilder.Entity<Service>()
                .Property(service => service.Price)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Reservation>()
                .HasRequired(reservation => reservation.Customer)
                .WithMany()
                .HasForeignKey(reservation => reservation.CustomerId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Reservation>()
                .HasRequired(reservation => reservation.Room)
                .WithMany()
                .HasForeignKey(reservation => reservation.RoomId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Reservation>()
                .HasOptional(reservation => reservation.CreatedByUser)
                .WithMany()
                .HasForeignKey(reservation => reservation.CreatedByUserId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<RoomSession>()
                .HasRequired(session => session.Customer)
                .WithMany()
                .HasForeignKey(session => session.CustomerId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<RoomSession>()
                .HasRequired(session => session.Room)
                .WithMany()
                .HasForeignKey(session => session.RoomId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<RoomSession>()
                .HasOptional(session => session.Reservation)
                .WithMany()
                .HasForeignKey(session => session.ReservationId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<RoomSession>()
                .Property(session => session.HourlyRate)
                .HasPrecision(18, 2);
        }
    }
}
