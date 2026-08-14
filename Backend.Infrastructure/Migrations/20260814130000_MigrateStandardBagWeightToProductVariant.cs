using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations;

/// <summary>
/// Chuyển cấu hình đóng bao cũ StandardBagWeightKg:{ProductVariantId}
/// sang ProductVariant.Weight và ẩn các cấu hình cũ khỏi màn hình quản trị.
/// </summary>
[DbContext(typeof(BackendContext))]
[Migration("20260814130000_MigrateStandardBagWeightToProductVariant")]
public sealed class MigrateStandardBagWeightToProductVariant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE `ProductVariant` AS pv
            INNER JOIN `SystemConfig` AS sc
                ON sc.`ConfigKey` = CONCAT('StandardBagWeightKg:', pv.`Id`)
               AND sc.`IsDeleted` = 0
            SET pv.`Weight` = CAST(sc.`ConfigValue` AS DECIMAL(18, 3)),
                pv.`LastModifiedDate` = NOW()
            WHERE sc.`ConfigValue` REGEXP '^[[:space:]]*[0-9]+([.][0-9]+)?[[:space:]]*$'
              AND CAST(sc.`ConfigValue` AS DECIMAL(18, 3)) > 0;

            UPDATE `SystemConfig`
            SET `IsDeleted` = 1,
                `LastModifiedDate` = NOW()
            WHERE `ConfigKey` LIKE 'StandardBagWeightKg:%'
              AND `IsDeleted` = 0;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Khôi phục khả năng nhìn thấy cấu hình cũ; không ghi đè Weight vì có thể
        // quản trị viên đã cập nhật biến thể sau khi migration được áp dụng.
        migrationBuilder.Sql(
            """
            UPDATE `SystemConfig`
            SET `IsDeleted` = 0,
                `LastModifiedDate` = NOW()
            WHERE `ConfigKey` LIKE 'StandardBagWeightKg:%'
              AND `IsDeleted` = 1;
            """);
    }
}
