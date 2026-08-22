using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Migrations;

/// <summary>
/// Persists the bag weight at inspection time so historical inspection totals
/// are not changed when outbound processing later consumes the physical bag.
/// </summary>
[DbContext(typeof(BackendContext))]
[Migration("20260822150000_AddQualityInspectionBagWeightSnapshot")]
public partial class AddQualityInspectionBagWeightSnapshot : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "BagWeightSnapshotKg",
            table: "QualityInspectionBagResult",
            type: "decimal(18,3)",
            precision: 18,
            scale: 3,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "BagWeightSnapshotKg",
            table: "QualityInspectionBagResult");
    }
}
