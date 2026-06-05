using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Action",
                columns: new[] { "Id", "CreatedBy", "Description", "LastModifiedDate", "Name", "UpdatedBy" },
                values: new object[,]
                {
                    { 1001, null, null, null, "Thêm mới", null },
                    { 1002, null, null, null, "Xem", null },
                    { 1003, null, null, null, "Chỉnh sửa", null },
                    { 1004, null, null, null, "Xoá", null },
                    { 1005, null, null, null, "Xuất dữ liệu", null },
                    { 1006, null, null, null, "Duyệt", null }
                });

            migrationBuilder.InsertData(
                table: "Role",
                columns: new[] { "Id", "CreatedBy", "Description", "LastModifiedDate", "Name", "UpdatedBy" },
                values: new object[,]
                {
                    { 1001, null, null, null, "Quản trị viên", null },
                    { 1002, null, null, null, "Người dùng", null }
                });

            migrationBuilder.InsertData(
                table: "UserStatus",
                columns: new[] { "Id", "Color", "CreatedBy", "Description", "LastModifiedDate", "Name", "UpdatedBy" },
                values: new object[,]
                {
                    { 1001, "#ff9500", null, null, null, "Chưa kích hoạt", null },
                    { 1002, "#00b315", null, null, null, "Đang hoạt động", null },
                    { 1003, "#ff0000", null, null, null, "Bị khoá", null },
                    { 1004, "#787878", null, null, null, "Ngưng hoạt động", null }
                });

            migrationBuilder.InsertData(
                table: "User",
                columns: new[] { "Id", "AddresDetail", "AvatarId", "CreatedBy", "DateOfBirth", "DateOfIssue", "Email", "FirstName", "Gender", "IdentityNumber", "LastLoginDate", "LastModifiedDate", "LastName", "LockEndDate", "MicrosoftId", "PasswordHash", "PhoneNumber", "PlaceOfIssue", "ProvinceId", "UpdatedBy", "UserStatusId", "Username", "WardId" },
                values: new object[] { 1001, null, null, null, null, null, "systemadmin@gmail.vn", "System", 1, null, null, null, "Admin", null, null, "$2a$11$kH4XY8m7bRFmUHhvuMlznOhLH74exbW2sjXnO0TSOkDQK4q/0gfVG", null, null, null, null, 1002, "admin", null });

            migrationBuilder.InsertData(
                table: "UserRole",
                columns: new[] { "Id", "CreatedBy", "LastModifiedDate", "RoleId", "UpdatedBy", "UserId" },
                values: new object[] { 1001, null, null, 1001, null, 1001 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Action",
                keyColumn: "Id",
                keyValue: 1001);

            migrationBuilder.DeleteData(
                table: "Action",
                keyColumn: "Id",
                keyValue: 1002);

            migrationBuilder.DeleteData(
                table: "Action",
                keyColumn: "Id",
                keyValue: 1003);

            migrationBuilder.DeleteData(
                table: "Action",
                keyColumn: "Id",
                keyValue: 1004);

            migrationBuilder.DeleteData(
                table: "Action",
                keyColumn: "Id",
                keyValue: 1005);

            migrationBuilder.DeleteData(
                table: "Action",
                keyColumn: "Id",
                keyValue: 1006);

            migrationBuilder.DeleteData(
                table: "Role",
                keyColumn: "Id",
                keyValue: 1002);

            migrationBuilder.DeleteData(
                table: "UserRole",
                keyColumn: "Id",
                keyValue: 1001);

            migrationBuilder.DeleteData(
                table: "UserStatus",
                keyColumn: "Id",
                keyValue: 1001);

            migrationBuilder.DeleteData(
                table: "UserStatus",
                keyColumn: "Id",
                keyValue: 1003);

            migrationBuilder.DeleteData(
                table: "UserStatus",
                keyColumn: "Id",
                keyValue: 1004);

            migrationBuilder.DeleteData(
                table: "Role",
                keyColumn: "Id",
                keyValue: 1001);

            migrationBuilder.DeleteData(
                table: "User",
                keyColumn: "Id",
                keyValue: 1001);

            migrationBuilder.DeleteData(
                table: "UserStatus",
                keyColumn: "Id",
                keyValue: 1002);
        }
    }
}
