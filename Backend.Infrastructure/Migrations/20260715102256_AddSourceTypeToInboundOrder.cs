using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceTypeToInboundOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Kiểm tra cột trước khi thêm (tránh lỗi nếu cột đã tồn tại từ một lần chạy khác)
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'InboundOrder';
                SET @colname = 'SourceType';
                SET @prepstr = IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                     WHERE TABLE_SCHEMA = @dbname
                       AND TABLE_NAME   = @tablename
                       AND COLUMN_NAME  = @colname) = 0,
                    CONCAT('ALTER TABLE `', @tablename, '` ADD COLUMN `', @colname, '` VARCHAR(20) NULL'),
                    'SELECT 1'
                );
                PREPARE stmt FROM @prepstr;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Kiểm tra cột trước khi xóa (tránh lỗi nếu cột không tồn tại)
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'InboundOrder';
                SET @colname = 'SourceType';
                SET @prepstr = IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                     WHERE TABLE_SCHEMA = @dbname
                       AND TABLE_NAME   = @tablename
                       AND COLUMN_NAME  = @colname) > 0,
                    CONCAT('ALTER TABLE `', @tablename, '` DROP COLUMN `', @colname, '`'),
                    'SELECT 1'
                );
                PREPARE stmt FROM @prepstr;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");
        }
    }
}
