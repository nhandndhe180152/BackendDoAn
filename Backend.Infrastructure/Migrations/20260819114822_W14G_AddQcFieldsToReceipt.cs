using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class W14G_AddQcFieldsToReceipt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AcceptedWeightKg",
                table: "PaddyPurchaseReceipt",
                type: "decimal(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DebtDueDate",
                table: "PaddyPurchaseReceipt",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "QcFinalizedAt",
                table: "PaddyPurchaseReceipt",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QcFinalizedBy",
                table: "PaddyPurchaseReceipt",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RejectedWeightKg",
                table: "PaddyPurchaseReceipt",
                type: "decimal(18,3)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptedWeightKg",
                table: "PaddyPurchaseReceipt");

            migrationBuilder.DropColumn(
                name: "DebtDueDate",
                table: "PaddyPurchaseReceipt");

            migrationBuilder.DropColumn(
                name: "QcFinalizedAt",
                table: "PaddyPurchaseReceipt");

            migrationBuilder.DropColumn(
                name: "QcFinalizedBy",
                table: "PaddyPurchaseReceipt");

            migrationBuilder.DropColumn(
                name: "RejectedWeightKg",
                table: "PaddyPurchaseReceipt");
        }
    }
}
