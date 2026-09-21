namespace MusicBoxManagement.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddPermissionRolePermissionAuditLog : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.AuditLogs",
                c => new
                    {
                        AuditLogId = c.Int(nullable: false, identity: true),
                        ActorType = c.String(nullable: false),
                        UserId = c.String(maxLength: 128),
                        Action = c.String(nullable: false),
                        EntityName = c.String(nullable: false),
                        EntityId = c.String(),
                        Description = c.String(),
                        CreatedAt = c.DateTimeOffset(nullable: false, precision: 7),
                    })
                .PrimaryKey(t => t.AuditLogId)
                .ForeignKey("dbo.AspNetUsers", t => t.UserId)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.Permissions",
                c => new
                    {
                        PermissionId = c.Int(nullable: false, identity: true),
                        Code = c.String(nullable: false, maxLength: 128),
                        Name = c.String(nullable: false),
                    })
                .PrimaryKey(t => t.PermissionId)
                .Index(t => t.Code, unique: true, name: "IX_Permission_Code");
            
            CreateTable(
                "dbo.RolePermissions",
                c => new
                    {
                        RoleId = c.String(nullable: false, maxLength: 128),
                        PermissionId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.RoleId, t.PermissionId })
                .ForeignKey("dbo.Permissions", t => t.PermissionId)
                .ForeignKey("dbo.AspNetRoles", t => t.RoleId)
                .Index(t => t.RoleId)
                .Index(t => t.PermissionId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.RolePermissions", "RoleId", "dbo.AspNetRoles");
            DropForeignKey("dbo.RolePermissions", "PermissionId", "dbo.Permissions");
            DropForeignKey("dbo.AuditLogs", "UserId", "dbo.AspNetUsers");
            DropIndex("dbo.RolePermissions", new[] { "PermissionId" });
            DropIndex("dbo.RolePermissions", new[] { "RoleId" });
            DropIndex("dbo.Permissions", "IX_Permission_Code");
            DropIndex("dbo.AuditLogs", new[] { "UserId" });
            DropTable("dbo.RolePermissions");
            DropTable("dbo.Permissions");
            DropTable("dbo.AuditLogs");
        }
    }
}
