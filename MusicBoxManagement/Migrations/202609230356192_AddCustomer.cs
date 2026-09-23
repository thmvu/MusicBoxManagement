namespace MusicBoxManagement.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddCustomer : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Customers",
                c => new
                    {
                        CustomerId = c.Int(nullable: false, identity: true),
                        FullName = c.String(nullable: false, maxLength: 100),
                        PhoneNumber = c.String(nullable: false, maxLength: 10),
                    })
                .PrimaryKey(t => t.CustomerId)
                .Index(t => t.PhoneNumber, unique: true, name: "IX_Customer_PhoneNumber");

            Sql("ALTER TABLE dbo.Customers ADD CONSTRAINT CK_Customer_PhoneNumber CHECK (DATALENGTH(PhoneNumber) = 20 AND PhoneNumber LIKE '0%' AND PhoneNumber NOT LIKE '%[^0-9]%')");
            Sql("ALTER TABLE dbo.Customers ADD CONSTRAINT CK_Customer_FullName CHECK (LEN(LTRIM(RTRIM(FullName))) > 0)");
            
        }
        
        public override void Down()
        {
            DropIndex("dbo.Customers", "IX_Customer_PhoneNumber");
            DropTable("dbo.Customers");
        }
    }
}
