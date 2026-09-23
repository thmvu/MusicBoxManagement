namespace MusicBoxManagement.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddRoom : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Rooms",
                c => new
                    {
                        RoomId = c.Int(nullable: false, identity: true),
                        RoomCode = c.String(nullable: false, maxLength: 30),
                        RoomTypeId = c.Int(nullable: false),
                        Name = c.String(nullable: false, maxLength: 100),
                        ImageUrl = c.String(maxLength: 300),
                        Description = c.String(maxLength: 2000),
                        IsActive = c.Boolean(nullable: false),
                        InactiveReason = c.String(maxLength: 500),
                        CreatedAt = c.DateTimeOffset(nullable: false, precision: 7),
                    })
                .PrimaryKey(t => t.RoomId)
                .ForeignKey("dbo.RoomTypes", t => t.RoomTypeId)
                .Index(t => t.RoomCode, unique: true, name: "IX_Room_RoomCode")
                .Index(t => t.RoomTypeId);

            Sql("ALTER TABLE dbo.Rooms ADD CONSTRAINT CK_Room_InactiveReason CHECK (IsActive = 1 OR LEN(LTRIM(RTRIM(ISNULL(InactiveReason, '')))) > 0)");
            Sql("ALTER TABLE dbo.Rooms ADD CONSTRAINT CK_Room_RoomCode CHECK (LEN(LTRIM(RTRIM(RoomCode))) > 0)");
            Sql("ALTER TABLE dbo.Rooms ADD CONSTRAINT CK_Room_Name CHECK (LEN(LTRIM(RTRIM(Name))) > 0)");
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.Rooms", "RoomTypeId", "dbo.RoomTypes");
            DropIndex("dbo.Rooms", new[] { "RoomTypeId" });
            DropIndex("dbo.Rooms", "IX_Room_RoomCode");
            DropTable("dbo.Rooms");
        }
    }
}
