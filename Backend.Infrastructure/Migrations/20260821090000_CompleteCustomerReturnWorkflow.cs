using System;
using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations;

// Superseded by the EF-scaffolded SyncCompletedCustomerReturnModel migration.
// Kept as a non-discoverable reference because this workspace may have the file open.
internal static class CompleteCustomerReturnWorkflowReference
{
    public static void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>("SubmittedAt", "CustomerReturnOrder", "datetime(6)", nullable: true);
        migrationBuilder.AddColumn<int>("SubmittedByUserId", "CustomerReturnOrder", "int", nullable: true);
        migrationBuilder.AddColumn<DateTime>("ReceivedAt", "CustomerReturnOrder", "datetime(6)", nullable: true);
        migrationBuilder.AddColumn<int>("ReceivedByUserId", "CustomerReturnOrder", "int", nullable: true);
        migrationBuilder.AddColumn<DateTime>("InspectedAt", "CustomerReturnOrder", "datetime(6)", nullable: true);
        migrationBuilder.AddColumn<int>("InspectedByUserId", "CustomerReturnOrder", "int", nullable: true);
        migrationBuilder.AddColumn<DateTime>("CancelledAt", "CustomerReturnOrder", "datetime(6)", nullable: true);
        migrationBuilder.AddColumn<int>("CancelledByUserId", "CustomerReturnOrder", "int", nullable: true);
        migrationBuilder.AddColumn<string>("CancellationReason", "CustomerReturnOrder", "varchar(500)", maxLength: 500, nullable: true);
        migrationBuilder.AddColumn<DateTime>("RejectedAt", "CustomerReturnOrder", "datetime(6)", nullable: true);
        migrationBuilder.AddColumn<int>("RejectedByUserId", "CustomerReturnOrder", "int", nullable: true);
        migrationBuilder.AddColumn<string>("RejectionReason", "CustomerReturnOrder", "varchar(500)", maxLength: 500, nullable: true);
        migrationBuilder.AddColumn<decimal>("RefundedAmount", "CustomerReturnOrder", "decimal(18,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<string>("RefundStatus", "CustomerReturnOrder", "varchar(30)", maxLength: 30, nullable: false, defaultValue: "NOT_APPLICABLE");

        migrationBuilder.AddColumn<decimal>("QuantityReceived", "CustomerReturnOrderItemAllocation", "decimal(18,3)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<string>("Disposition", "CustomerReturnOrderItemAllocation", "varchar(30)", maxLength: 30, nullable: false, defaultValue: "PENDING_INSPECTION");
        migrationBuilder.AddColumn<int>("RejectedLocationId", "CustomerReturnOrderItemAllocation", "int", nullable: true);
        migrationBuilder.AddColumn<string>("RejectionReason", "CustomerReturnOrderItemAllocation", "varchar(500)", maxLength: 500, nullable: true);

        migrationBuilder.CreateIndex("IX_CustomerReturnOrderItemAllocation_RejectedLocationId", "CustomerReturnOrderItemAllocation", "RejectedLocationId");
        migrationBuilder.AddForeignKey("FK_CustomerReturnOrderItemAllocation_Location_RejectedLocationId", "CustomerReturnOrderItemAllocation", "RejectedLocationId", "Location", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddCheckConstraint("CK_CustomerReturnAllocation_Quantities", "CustomerReturnOrderItemAllocation", "QuantityReturned > 0 AND QuantityReceived >= 0 AND QuantityGood >= 0 AND QuantityDamaged >= 0 AND QuantityRejected >= 0 AND CreditQuantity >= 0");
        migrationBuilder.AddCheckConstraint("CK_CustomerReturnAllocation_CreditAmount", "CustomerReturnOrderItemAllocation", "CreditAmount >= 0 AND UnitCreditPrice >= 0");

        migrationBuilder.InsertData("CustomerReturnOrderStatus",
            new[] { "Id", "Code", "Name", "Color", "CreatedDate", "IsDeleted" },
            new object[,]
            {
                { 6, "PENDING_APPROVAL", "Chờ duyệt", "#8B5CF6", new DateTime(2026, 1, 1), false },
                { 7, "RECEIVED", "Đã nhận hàng", "#06B6D4", new DateTime(2026, 1, 1), false },
                { 8, "REJECTED", "Từ chối", "#DC2626", new DateTime(2026, 1, 1), false }
            });
        migrationBuilder.UpdateData("CustomerReturnOrderStatus", "Id", 4, "Name", "Hoàn tất");
    }

    public static void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_CustomerReturnOrderItemAllocation_Location_RejectedLocationId", "CustomerReturnOrderItemAllocation");
        migrationBuilder.DropIndex("IX_CustomerReturnOrderItemAllocation_RejectedLocationId", "CustomerReturnOrderItemAllocation");
        migrationBuilder.DropCheckConstraint("CK_CustomerReturnAllocation_Quantities", "CustomerReturnOrderItemAllocation");
        migrationBuilder.DropCheckConstraint("CK_CustomerReturnAllocation_CreditAmount", "CustomerReturnOrderItemAllocation");
        migrationBuilder.DeleteData("CustomerReturnOrderStatus", "Id", 6);
        migrationBuilder.DeleteData("CustomerReturnOrderStatus", "Id", 7);
        migrationBuilder.DeleteData("CustomerReturnOrderStatus", "Id", 8);
        migrationBuilder.UpdateData("CustomerReturnOrderStatus", "Id", 4, "Name", "Đã nhận lại hàng");

        foreach (var column in new[] { "QuantityReceived", "Disposition", "RejectedLocationId", "RejectionReason" })
            migrationBuilder.DropColumn(column, "CustomerReturnOrderItemAllocation");
        foreach (var column in new[] { "SubmittedAt", "SubmittedByUserId", "ReceivedAt", "ReceivedByUserId", "InspectedAt", "InspectedByUserId", "CancelledAt", "CancelledByUserId", "CancellationReason", "RejectedAt", "RejectedByUserId", "RejectionReason", "RefundedAmount", "RefundStatus" })
            migrationBuilder.DropColumn(column, "CustomerReturnOrder");
    }
}
