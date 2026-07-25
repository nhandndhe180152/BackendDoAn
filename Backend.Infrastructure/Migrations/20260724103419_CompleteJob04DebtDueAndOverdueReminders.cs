using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompleteJob04DebtDueAndOverdueReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
SET @s = (SELECT IF(
    (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'Alert' AND column_name = 'ConditionFingerprint') > 0,
    'ALTER TABLE Alert DROP COLUMN ConditionFingerprint',
    'SELECT 1'
));
PREPARE stmt FROM @s;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @s = (SELECT IF(
    (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'Alert' AND column_name = 'ResolvedReason') > 0,
    'ALTER TABLE Alert DROP COLUMN ResolvedReason',
    'SELECT 1'
));
PREPARE stmt FROM @s;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
");

            migrationBuilder.AddColumn<string>(
                name: "ConditionFingerprint",
                table: "Alert",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ResolvedReason",
                table: "Alert",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // Seed default configurations for JOB-04
            migrationBuilder.Sql("INSERT INTO SystemConfig (ConfigKey, ConfigValue, Name, Description, CreatedBy, CreatedDate, IsDeleted) VALUES " +
                "('DebtReminderLeadDays', '7', 'Số ngày báo trước công nợ sắp đến hạn', 'Số ngày báo trước khi công nợ sắp đến hạn phải trả hoặc phải thu', 1, NOW(), 0), " +
                "('DebtDueTodaySeverity', 'WARNING', 'Mức độ nghiêm trọng nợ đến hạn hôm nay', 'Mức độ nghiêm trọng của cảnh báo khi nợ đến hạn hôm nay (INFO/WARNING/CRITICAL)', 1, NOW(), 0), " +
                "('DebtOverdueWarningDays', '1', 'Số ngày quá hạn - Cảnh báo thường', 'Số ngày quá hạn tối thiểu để nâng mức cảnh báo lên WARNING', 1, NOW(), 0), " +
                "('DebtOverdueCriticalDays', '30', 'Số ngày quá hạn - Cảnh báo nghiêm trọng', 'Số ngày quá hạn tối thiểu để nâng mức cảnh báo lên CRITICAL', 1, NOW(), 0), " +
                "('DebtOverdueWarningAmount', '10000000', 'Số tiền quá hạn - Cảnh báo thường', 'Số tiền quá hạn tối thiểu để nâng mức cảnh báo lên WARNING', 1, NOW(), 0), " +
                "('DebtOverdueCriticalAmount', '50000000', 'Số tiền quá hạn - Cảnh báo nghiêm trọng', 'Số tiền quá hạn tối thiểu để nâng mức cảnh báo lên CRITICAL', 1, NOW(), 0) " +
                "ON DUPLICATE KEY UPDATE ConfigValue = VALUES(ConfigValue);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConditionFingerprint",
                table: "Alert");

            migrationBuilder.DropColumn(
                name: "ResolvedReason",
                table: "Alert");
        }
    }
}
