namespace MusicBoxManagement.Migrations
{
    using System;
    using System.Data.Entity.Migrations;

    public partial class AddOrder : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.OrderItems",
                c => new
                    {
                        OrderItemId = c.Int(nullable: false, identity: true),
                        OrderId = c.Int(nullable: false),
                        ServiceId = c.Int(nullable: false),
                        ServiceNameSnapshot = c.String(nullable: false, maxLength: 100),
                        Quantity = c.Int(nullable: false),
                        UnitPrice = c.Decimal(nullable: false, precision: 18, scale: 2),
                    })
                .PrimaryKey(t => t.OrderItemId)
                .ForeignKey("dbo.Orders", t => t.OrderId)
                .ForeignKey("dbo.Services", t => t.ServiceId)
                .Index(t => t.OrderId)
                .Index(t => t.ServiceId);

            CreateTable(
                "dbo.Orders",
                c => new
                    {
                        OrderId = c.Int(nullable: false, identity: true),
                        RoomSessionId = c.Int(nullable: false),
                        CreatedByUserId = c.String(maxLength: 128),
                        Status = c.String(nullable: false, maxLength: 20),
                        CreatedAt = c.DateTimeOffset(nullable: false, precision: 7),
                    })
                .PrimaryKey(t => t.OrderId)
                .ForeignKey("dbo.AspNetUsers", t => t.CreatedByUserId)
                .ForeignKey("dbo.RoomSessions", t => t.RoomSessionId)
                .Index(t => t.RoomSessionId)
                .Index(t => t.CreatedByUserId);

            CreateIndex("dbo.OrderItems", new[] { "OrderId", "ServiceId" }, unique: true, name: "UX_OrderItem_OrderService");
            CreateIndex("dbo.Orders", new[] { "RoomSessionId", "Status", "CreatedAt" }, name: "IX_Order_SessionStatusTime");
            Sql("ALTER TABLE dbo.Orders ADD CONSTRAINT CK_Order_Status CHECK (Status IN ('Pending', 'Completed', 'Cancelled'))");
            Sql("ALTER TABLE dbo.OrderItems ADD CONSTRAINT CK_OrderItem_Quantity CHECK (Quantity BETWEEN 1 AND 10)");
            Sql("ALTER TABLE dbo.OrderItems ADD CONSTRAINT CK_OrderItem_UnitPrice CHECK (UnitPrice > 0 AND UnitPrice = FLOOR(UnitPrice))");

        }

        public override void Down()
        {
            DropIndex("dbo.Orders", "IX_Order_SessionStatusTime");
            DropIndex("dbo.OrderItems", "UX_OrderItem_OrderService");
            DropForeignKey("dbo.OrderItems", "ServiceId", "dbo.Services");
            DropForeignKey("dbo.OrderItems", "OrderId", "dbo.Orders");
            DropForeignKey("dbo.Orders", "RoomSessionId", "dbo.RoomSessions");
            DropForeignKey("dbo.Orders", "CreatedByUserId", "dbo.AspNetUsers");
            DropIndex("dbo.Orders", new[] { "CreatedByUserId" });
            DropIndex("dbo.Orders", new[] { "RoomSessionId" });
            DropIndex("dbo.OrderItems", new[] { "ServiceId" });
            DropIndex("dbo.OrderItems", new[] { "OrderId" });
            DropTable("dbo.Orders");
            DropTable("dbo.OrderItems");
        }
    }
}
