using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Code_To_Lookups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "UserStatus",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "StockTakeStatus",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Role",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Menu",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Action",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "Action",
                keyColumn: "Id",
                keyValue: 1001,
                column: "Code",
                value: "CREATE");

            migrationBuilder.UpdateData(
                table: "Action",
                keyColumn: "Id",
                keyValue: 1002,
                column: "Code",
                value: "READ");

            migrationBuilder.UpdateData(
                table: "Action",
                keyColumn: "Id",
                keyValue: 1003,
                column: "Code",
                value: "UPDATE");

            migrationBuilder.UpdateData(
                table: "Action",
                keyColumn: "Id",
                keyValue: 1004,
                column: "Code",
                value: "DELETE");

            migrationBuilder.UpdateData(
                table: "Action",
                keyColumn: "Id",
                keyValue: 1005,
                column: "Code",
                value: "EXPORT");

            migrationBuilder.UpdateData(
                table: "Action",
                keyColumn: "Id",
                keyValue: 1006,
                column: "Code",
                value: "APPROVE");

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "Id",
                keyValue: 1001,
                column: "Code",
                value: "ADMIN");

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "Id",
                keyValue: 1002,
                column: "Code",
                value: "END_USER");

            migrationBuilder.UpdateData(
                table: "StockTakeStatus",
                keyColumn: "Id",
                keyValue: 1,
                column: "Code",
                value: "Draft");

            migrationBuilder.UpdateData(
                table: "StockTakeStatus",
                keyColumn: "Id",
                keyValue: 2,
                column: "Code",
                value: "Submitted");

            migrationBuilder.UpdateData(
                table: "StockTakeStatus",
                keyColumn: "Id",
                keyValue: 3,
                column: "Code",
                value: "Approved");

            migrationBuilder.UpdateData(
                table: "StockTakeStatus",
                keyColumn: "Id",
                keyValue: 4,
                column: "Code",
                value: "Rejected");

            migrationBuilder.UpdateData(
                table: "UserStatus",
                keyColumn: "Id",
                keyValue: 1001,
                column: "Code",
                value: "NotActivated");

            migrationBuilder.UpdateData(
                table: "UserStatus",
                keyColumn: "Id",
                keyValue: 1002,
                column: "Code",
                value: "Actived");

            migrationBuilder.UpdateData(
                table: "UserStatus",
                keyColumn: "Id",
                keyValue: 1003,
                column: "Code",
                value: "Locked");

            migrationBuilder.UpdateData(
                table: "UserStatus",
                keyColumn: "Id",
                keyValue: 1004,
                column: "Code",
                value: "Deactivated");

            migrationBuilder.CreateIndex(
                name: "IX_UserStatus_Code",
                table: "UserStatus",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_StockTakeStatus_Code",
                table: "StockTakeStatus",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_Role_Code",
                table: "Role",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_Menu_Code",
                table: "Menu",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_Action_Code",
                table: "Action",
                column: "Code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserStatus_Code",
                table: "UserStatus");

            migrationBuilder.DropIndex(
                name: "IX_StockTakeStatus_Code",
                table: "StockTakeStatus");

            migrationBuilder.DropIndex(
                name: "IX_Role_Code",
                table: "Role");

            migrationBuilder.DropIndex(
                name: "IX_Menu_Code",
                table: "Menu");

            migrationBuilder.DropIndex(
                name: "IX_Action_Code",
                table: "Action");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "UserStatus");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "StockTakeStatus");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Role");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Menu");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Action");
        }
    }
}
