using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations;

/// <summary>
/// Forward data fix for the location-scoped detached open-bag invariant.
/// The duplicate preflight intentionally fails the migration instead of merging data.
/// </summary>
public partial class ScopeOpenBagKeyByLocation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `__Mig_CheckOpenBagDuplicates`;");
        migrationBuilder.Sql(@"
CREATE PROCEDURE `__Mig_CheckOpenBagDuplicates`()
BEGIN
    IF EXISTS (
        SELECT 1
        FROM `PaddyLotBag` b
        INNER JOIN `PaddyLot` l ON l.`Id` = b.`LotId`
        WHERE b.`Status` = 'Stored'
          AND b.`BagKind` = 'Finished'
          AND b.`IsFull` = 0
          AND b.`IsDeleted` = 0
          AND b.`LocationId` IS NOT NULL
        GROUP BY l.`ProductVariantId`, l.`WarehouseId`, b.`LocationId`
        HAVING COUNT(*) > 1
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'OpenBagKey migration blocked: duplicate active open bags at the same variant/warehouse/location.';
    END IF;
END;");
        migrationBuilder.Sql("CALL `__Mig_CheckOpenBagDuplicates`();");
        migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `__Mig_CheckOpenBagDuplicates`;");

        migrationBuilder.Sql(@"
UPDATE `PaddyLotBag` b
INNER JOIN `PaddyLot` l ON l.`Id` = b.`LotId`
SET b.`OpenBagKey` = CONCAT(l.`ProductVariantId`, ':', l.`WarehouseId`, ':', b.`LocationId`),
    b.`StackOrder` = 0
WHERE b.`Status` = 'Stored'
  AND b.`BagKind` = 'Finished'
  AND b.`IsFull` = 0
  AND b.`IsDeleted` = 0
  AND b.`LocationId` IS NOT NULL;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Deliberately irreversible: restoring the global key can reintroduce collisions.
    }
}
