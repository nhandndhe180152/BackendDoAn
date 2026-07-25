using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompleteJob05FcmNotificationRetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "FcmNotificationLog",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DeduplicationKey",
                table: "FcmNotificationLog",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "FcmNotificationLog",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAttemptAt",
                table: "FcmNotificationLog",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastErrorCode",
                table: "FcmNotificationLog",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "NextRetryAt",
                table: "FcmNotificationLog",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessingBy",
                table: "FcmNotificationLog",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessingLeaseUntil",
                table: "FcmNotificationLog",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessingStartedAt",
                table: "FcmNotificationLog",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderMessageId",
                table: "FcmNotificationLog",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "FcmNotificationLog",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "PENDING")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_FcmNotificationLog_DeduplicationKey",
                table: "FcmNotificationLog",
                column: "DeduplicationKey");

            migrationBuilder.CreateIndex(
                name: "IX_FcmNotificationLog_EligibleQuery",
                table: "FcmNotificationLog",
                columns: new[] { "Status", "IsDeleted", "NextRetryAt", "Id" });

            // Backfill historical logs
            migrationBuilder.Sql("UPDATE FcmNotificationLog SET Status = 'SENT' WHERE IsSent = 1;");
            migrationBuilder.Sql("UPDATE FcmNotificationLog SET Status = 'PENDING', AttemptCount = 1 WHERE IsSent = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FcmNotificationLog_DeduplicationKey",
                table: "FcmNotificationLog");

            migrationBuilder.DropIndex(
                name: "IX_FcmNotificationLog_EligibleQuery",
                table: "FcmNotificationLog");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "FcmNotificationLog");

            migrationBuilder.DropColumn(
                name: "DeduplicationKey",
                table: "FcmNotificationLog");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "FcmNotificationLog");

            migrationBuilder.DropColumn(
                name: "LastAttemptAt",
                table: "FcmNotificationLog");

            migrationBuilder.DropColumn(
                name: "LastErrorCode",
                table: "FcmNotificationLog");

            migrationBuilder.DropColumn(
                name: "NextRetryAt",
                table: "FcmNotificationLog");

            migrationBuilder.DropColumn(
                name: "ProcessingBy",
                table: "FcmNotificationLog");

            migrationBuilder.DropColumn(
                name: "ProcessingLeaseUntil",
                table: "FcmNotificationLog");

            migrationBuilder.DropColumn(
                name: "ProcessingStartedAt",
                table: "FcmNotificationLog");

            migrationBuilder.DropColumn(
                name: "ProviderMessageId",
                table: "FcmNotificationLog");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "FcmNotificationLog");
        }
    }
}
