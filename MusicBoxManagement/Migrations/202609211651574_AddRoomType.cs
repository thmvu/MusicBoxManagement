namespace MusicBoxManagement.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddRoomType : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.RoomTypes",
                c => new
                    {
                        RoomTypeId = c.Int(nullable: false, identity: true),
                        Code = c.String(nullable: false, maxLength: 20),
                        Name = c.String(nullable: false, maxLength: 100),
                        Capacity = c.Int(nullable: false),
                        PricePerHour = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Amenities = c.String(nullable: false, maxLength: 1000),
                        Description = c.String(maxLength: 2000),
                    })
                .PrimaryKey(t => t.RoomTypeId)
                .Index(t => t.Code, unique: true, name: "IX_RoomType_Code");

            Sql("ALTER TABLE dbo.RoomTypes ADD CONSTRAINT CK_RoomType_Code CHECK (Code IN ('STANDARD', 'VIP'))");
            Sql("ALTER TABLE dbo.RoomTypes ADD CONSTRAINT CK_RoomType_Capacity CHECK (Capacity > 0)");
            Sql("ALTER TABLE dbo.RoomTypes ADD CONSTRAINT CK_RoomType_PricePerHour CHECK (PricePerHour > 0 AND PricePerHour = FLOOR(PricePerHour))");
            
        }
        
        public override void Down()
        {
            DropIndex("dbo.RoomTypes", "IX_RoomType_Code");
            DropTable("dbo.RoomTypes");
        }
    }
}
