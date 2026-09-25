namespace MusicBoxManagement.Migrations
{
    using System;
    using System.Data.Entity.Migrations;

    public partial class AddRoomSession : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.RoomSessions",
                c => new
                    {
                        RoomSessionId = c.Int(nullable: false, identity: true),
                        CustomerId = c.Int(nullable: false),
                        RoomId = c.Int(nullable: false),
                        ReservationId = c.Int(),
                        ActualStartTime = c.DateTimeOffset(nullable: false, precision: 7),
                        ExpectedEndTime = c.DateTimeOffset(precision: 7),
                        ActualEndTime = c.DateTimeOffset(precision: 7),
                        HourlyRate = c.Decimal(nullable: false, precision: 18, scale: 2),
                        RoomCodeSnapshot = c.String(nullable: false, maxLength: 30),
                        RoomTypeCodeSnapshot = c.String(nullable: false, maxLength: 20),
                        RoomTypeNameSnapshot = c.String(nullable: false, maxLength: 100),
                        Status = c.String(nullable: false, maxLength: 20),
                    })
                .PrimaryKey(t => t.RoomSessionId)
                .ForeignKey("dbo.Customers", t => t.CustomerId)
                .ForeignKey("dbo.Reservations", t => t.ReservationId)
                .ForeignKey("dbo.Rooms", t => t.RoomId)
                .Index(t => t.CustomerId)
                .Index(t => t.RoomId);

            Sql("CREATE UNIQUE INDEX UX_RoomSession_ActiveRoom ON dbo.RoomSessions(RoomId) WHERE Status = 'Active'");
            Sql("CREATE UNIQUE INDEX UX_RoomSession_ActiveCustomer ON dbo.RoomSessions(CustomerId) WHERE Status = 'Active'");
            Sql("CREATE UNIQUE INDEX UX_RoomSession_Reservation ON dbo.RoomSessions(ReservationId) WHERE ReservationId IS NOT NULL");
            CreateIndex("dbo.RoomSessions", new[] { "RoomId", "Status", "ActualStartTime", "ExpectedEndTime" }, name: "IX_RoomSession_RoomSchedule");
            CreateIndex("dbo.RoomSessions", new[] { "CustomerId", "Status", "ActualStartTime", "ExpectedEndTime" }, name: "IX_RoomSession_CustomerSchedule");
            Sql("ALTER TABLE dbo.RoomSessions ADD CONSTRAINT CK_RoomSession_Status CHECK (Status IN ('Active', 'Completed'))");
            Sql("ALTER TABLE dbo.RoomSessions ADD CONSTRAINT CK_RoomSession_ActualEnd CHECK ((Status = 'Active' AND ActualEndTime IS NULL) OR (Status = 'Completed' AND ActualEndTime IS NOT NULL AND ActualEndTime >= ActualStartTime))");
            Sql("ALTER TABLE dbo.RoomSessions ADD CONSTRAINT CK_RoomSession_ExpectedEnd CHECK ((ReservationId IS NULL AND ExpectedEndTime IS NULL) OR (ReservationId IS NOT NULL AND ExpectedEndTime > ActualStartTime))");
            Sql("ALTER TABLE dbo.RoomSessions ADD CONSTRAINT CK_RoomSession_HourlyRate CHECK (HourlyRate > 0 AND HourlyRate = FLOOR(HourlyRate))");

        }

        public override void Down()
        {
            DropIndex("dbo.RoomSessions", "IX_RoomSession_CustomerSchedule");
            DropIndex("dbo.RoomSessions", "IX_RoomSession_RoomSchedule");
            Sql("DROP INDEX UX_RoomSession_Reservation ON dbo.RoomSessions");
            Sql("DROP INDEX UX_RoomSession_ActiveCustomer ON dbo.RoomSessions");
            Sql("DROP INDEX UX_RoomSession_ActiveRoom ON dbo.RoomSessions");
            DropForeignKey("dbo.RoomSessions", "RoomId", "dbo.Rooms");
            DropForeignKey("dbo.RoomSessions", "ReservationId", "dbo.Reservations");
            DropForeignKey("dbo.RoomSessions", "CustomerId", "dbo.Customers");
            DropIndex("dbo.RoomSessions", new[] { "RoomId" });
            DropIndex("dbo.RoomSessions", new[] { "CustomerId" });
            DropTable("dbo.RoomSessions");
        }
    }
}
