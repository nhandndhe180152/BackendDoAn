using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompleteJob03LotQualityRecheckAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Seed configurations safely (using MySQL insert-ignore equivalent query check)
            migrationBuilder.Sql(@"
                INSERT INTO SystemConfig (ConfigKey, ConfigValue, Name, Description, IsDeleted)
                SELECT 'QualityInspectionIntervalDays', '30', 'Chu ky kiem dinh dinh ky (ngay)', 'Chu ky toi thieu giua cac lan kiem dinh chat luong', 0
                WHERE NOT EXISTS (SELECT 1 FROM SystemConfig WHERE ConfigKey = 'QualityInspectionIntervalDays');

                INSERT INTO SystemConfig (ConfigKey, ConfigValue, Name, Description, IsDeleted)
                SELECT 'InspectionOverdueCriticalDays', '7', 'Thoi gian tre han kiem dinh critical (ngay)', 'So ngay qua han kiem dinh toi da truoc khi dua len muc nguy hiem', 0
                WHERE NOT EXISTS (SELECT 1 FROM SystemConfig WHERE ConfigKey = 'InspectionOverdueCriticalDays');

                INSERT INTO SystemConfig (ConfigKey, ConfigValue, Name, Description, IsDeleted)
                SELECT 'MoistureWarningThreshold', '14.5', 'Nguong canh bao do am (%)', 'Do am toi da cho phep truoc khi canh bao', 0
                WHERE NOT EXISTS (SELECT 1 FROM SystemConfig WHERE ConfigKey = 'MoistureWarningThreshold');

                INSERT INTO SystemConfig (ConfigKey, ConfigValue, Name, Description, IsDeleted)
                SELECT 'MoistureCriticalThreshold', '16.0', 'Nguong nguy hiem do am (%)', 'Do am toi da cho phep truoc khi canh bao o muc nguy hiem', 0
                WHERE NOT EXISTS (SELECT 1 FROM SystemConfig WHERE ConfigKey = 'MoistureCriticalThreshold');

                INSERT INTO SystemConfig (ConfigKey, ConfigValue, Name, Description, IsDeleted)
                SELECT 'MoldWarningLevel', 'NHE', 'Muc moc canh bao', 'Muc moc toi da cho phep truoc khi canh bao (KHONG, NHE, NANG)', 0
                WHERE NOT EXISTS (SELECT 1 FROM SystemConfig WHERE ConfigKey = 'MoldWarningLevel');

                INSERT INTO SystemConfig (ConfigKey, ConfigValue, Name, Description, IsDeleted)
                SELECT 'MoldCriticalLevel', 'NANG', 'Muc moc nguy hiem', 'Muc moc toi da cho phep truoc khi canh bao nguy hiem (KHONG, NHE, NANG)', 0
                WHERE NOT EXISTS (SELECT 1 FROM SystemConfig WHERE ConfigKey = 'MoldCriticalLevel');

                INSERT INTO SystemConfig (ConfigKey, ConfigValue, Name, Description, IsDeleted)
                SELECT 'LongStoredWarningDays', '30', 'So ngay canh bao luu kho', 'So ngay toi da lua/gao duoc luu kho truoc khi canh bao', 0
                WHERE NOT EXISTS (SELECT 1 FROM SystemConfig WHERE ConfigKey = 'LongStoredWarningDays');

                INSERT INTO SystemConfig (ConfigKey, ConfigValue, Name, Description, IsDeleted)
                SELECT 'LongStoredCriticalDays', '60', 'So ngay nguy hiem luu kho', 'So ngay toi da lua/gao duoc luu kho truoc khi canh bao nguy hiem', 0
                WHERE NOT EXISTS (SELECT 1 FROM SystemConfig WHERE ConfigKey = 'LongStoredCriticalDays');
            ");

            migrationBuilder.CreateIndex(
                name: "IX_QualityInspection_Latest",
                table: "QualityInspection",
                columns: new[] { "PaddyLotId", "IsDeleted", "InspectedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QualityInspection_Latest",
                table: "QualityInspection");

            migrationBuilder.Sql(@"
                DELETE FROM SystemConfig WHERE ConfigKey IN (
                    'QualityInspectionIntervalDays',
                    'InspectionOverdueCriticalDays',
                    'MoistureWarningThreshold',
                    'MoistureCriticalThreshold',
                    'MoldWarningLevel',
                    'MoldCriticalLevel',
                    'LongStoredWarningDays',
                    'LongStoredCriticalDays'
                );
            ");
        }
    }
}
