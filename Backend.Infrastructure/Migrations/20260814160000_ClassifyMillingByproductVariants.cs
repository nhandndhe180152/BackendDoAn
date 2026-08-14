using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations;

/// <summary>
/// Chuẩn hóa các SKU phụ phẩm đã tạo trước khi form biến thể hỗ trợ IsByproduct.
/// </summary>
[DbContext(typeof(BackendContext))]
[Migration("20260814160000_ClassifyMillingByproductVariants")]
public sealed class ClassifyMillingByproductVariants : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE `ProductVariant`
            SET `IsByproduct` = 1,
                `LastModifiedDate` = NOW()
            WHERE `IsDeleted` = 0
              AND (`SKU` LIKE 'TAM-%' OR `SKU` LIKE 'CAM-%' OR `SKU` LIKE 'TRAU-%');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE `ProductVariant`
            SET `IsByproduct` = 0,
                `LastModifiedDate` = NOW()
            WHERE `IsDeleted` = 0
              AND (`SKU` LIKE 'TAM-%' OR `SKU` LIKE 'CAM-%' OR `SKU` LIKE 'TRAU-%');
            """);
    }
}
