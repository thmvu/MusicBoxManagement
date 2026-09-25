namespace MusicBoxManagement.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddService : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Services",
                c => new
                    {
                        ServiceId = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false, maxLength: 100),
                        Category = c.String(nullable: false, maxLength: 20),
                        Price = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Description = c.String(maxLength: 2000),
                        IsActive = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.ServiceId);

            Sql("ALTER TABLE dbo.Services ADD CONSTRAINT CK_Service_Category CHECK (Category IN (N'Đồ uống', N'Đồ ăn', N'Khác'))");
            Sql("ALTER TABLE dbo.Services ADD CONSTRAINT CK_Service_Price CHECK (Price > 0 AND Price = FLOOR(Price))");
            Sql("ALTER TABLE dbo.Services ADD CONSTRAINT CK_Service_Name CHECK (LEN(LTRIM(RTRIM(Name))) > 0)");
            
        }
        
        public override void Down()
        {
            DropTable("dbo.Services");
        }
    }
}
