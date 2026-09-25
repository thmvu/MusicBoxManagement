namespace MusicBoxManagement.Migrations
{
    using System;
    using System.Data.Entity.Migrations;

    public partial class AddReservation : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Reservations",
                c => new
                    {
                        ReservationId = c.Int(nullable: false, identity: true),
                        CustomerId = c.Int(nullable: false),
                        RoomId = c.Int(nullable: false),
                        StartTime = c.DateTimeOffset(nullable: false, precision: 7),
                        EndTime = c.DateTimeOffset(nullable: false, precision: 7),
                        Status = c.String(nullable: false, maxLength: 20),
                        CancellationReason = c.String(maxLength: 500),
                        CreatedByUserId = c.String(maxLength: 128),
                        CreatedAt = c.DateTimeOffset(nullable: false, precision: 7),
                    })
                .PrimaryKey(t => t.ReservationId)
                .ForeignKey("dbo.AspNetUsers", t => t.CreatedByUserId)
                .ForeignKey("dbo.Customers", t => t.CustomerId)
                .ForeignKey("dbo.Rooms", t => t.RoomId)
                .Index(t => t.CustomerId)
                .Index(t => t.RoomId)
                .Index(t => t.CreatedByUserId);

            CreateIndex("dbo.Reservations", new[] { "RoomId", "Status", "StartTime", "EndTime" }, name: "IX_Reservation_RoomSchedule");
            CreateIndex("dbo.Reservations", new[] { "CustomerId", "Status", "StartTime", "EndTime" }, name: "IX_Reservation_CustomerSchedule");
            Sql("ALTER TABLE dbo.Reservations ADD CONSTRAINT CK_Reservation_Time CHECK (StartTime < EndTime)");
            Sql("ALTER TABLE dbo.Reservations ADD CONSTRAINT CK_Reservation_Status CHECK (Status IN ('Confirmed', 'CheckedIn', 'Completed', 'Cancelled', 'NoShow'))");

        }

        public override void Down()
        {
            DropIndex("dbo.Reservations", "IX_Reservation_CustomerSchedule");
            DropIndex("dbo.Reservations", "IX_Reservation_RoomSchedule");
            DropForeignKey("dbo.Reservations", "RoomId", "dbo.Rooms");
            DropForeignKey("dbo.Reservations", "CustomerId", "dbo.Customers");
            DropForeignKey("dbo.Reservations", "CreatedByUserId", "dbo.AspNetUsers");
            DropIndex("dbo.Reservations", new[] { "CreatedByUserId" });
            DropIndex("dbo.Reservations", new[] { "RoomId" });
            DropIndex("dbo.Reservations", new[] { "CustomerId" });
            DropTable("dbo.Reservations");
        }
    }
}
