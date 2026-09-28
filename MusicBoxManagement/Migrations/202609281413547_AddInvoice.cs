namespace MusicBoxManagement.Migrations
{
    using System;
    using System.Data.Entity.Migrations;

    public partial class AddInvoice : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Invoices",
                c => new
                    {
                        InvoiceId = c.Int(nullable: false, identity: true),
                        InvoiceNumber = c.String(nullable: false, maxLength: 40),
                        RoomSessionId = c.Int(nullable: false),
                        RoomCharge = c.Decimal(nullable: false, precision: 18, scale: 2),
                        ServiceCharge = c.Decimal(nullable: false, precision: 18, scale: 2),
                        TotalAmount = c.Decimal(nullable: false, precision: 18, scale: 2),
                        PaymentMethod = c.String(nullable: false, maxLength: 20),
                        ProcessedByUserId = c.String(nullable: false, maxLength: 128),
                        ProcessedByNameSnapshot = c.String(nullable: false, maxLength: 100),
                        PaidAt = c.DateTimeOffset(nullable: false, precision: 7),
                    })
                .PrimaryKey(t => t.InvoiceId)
                .ForeignKey("dbo.AspNetUsers", t => t.ProcessedByUserId)
                .ForeignKey("dbo.RoomSessions", t => t.RoomSessionId)
                .Index(t => t.InvoiceNumber, unique: true, name: "UX_Invoice_Number")
                .Index(t => t.RoomSessionId, unique: true, name: "UX_Invoice_Session")
                .Index(t => t.ProcessedByUserId);
            Sql("ALTER TABLE dbo.Invoices ADD CONSTRAINT CK_Invoice_Amounts CHECK (RoomCharge >= 0 AND ServiceCharge >= 0 AND TotalAmount = RoomCharge + ServiceCharge)");
            Sql("ALTER TABLE dbo.Invoices ADD CONSTRAINT CK_Invoice_PaymentMethod CHECK (PaymentMethod IN ('Cash', 'BankTransfer'))");

        }

        public override void Down()
        {
            DropForeignKey("dbo.Invoices", "RoomSessionId", "dbo.RoomSessions");
            DropForeignKey("dbo.Invoices", "ProcessedByUserId", "dbo.AspNetUsers");
            DropIndex("dbo.Invoices", new[] { "ProcessedByUserId" });
            DropIndex("dbo.Invoices", "UX_Invoice_Session");
            DropIndex("dbo.Invoices", "UX_Invoice_Number");
            DropTable("dbo.Invoices");
        }
    }
}
