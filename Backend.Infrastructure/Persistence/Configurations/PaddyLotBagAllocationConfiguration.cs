using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class PaddyLotBagAllocationConfiguration : IEntityTypeConfiguration<PaddyLotBagAllocation>
{
    public void Configure(EntityTypeBuilder<PaddyLotBagAllocation> builder)
    {
        builder.ToTable("PaddyLotBagAllocation");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ReferenceType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.AllocatedWeightKg).HasPrecision(18, 3);
        builder.Property(x => x.ConsumedWeightKg).HasPrecision(18, 3);
        builder.Property(x => x.BagWeightSnapshotKg).HasPrecision(18, 3);
        builder.HasOne(x => x.Bag).WithMany(x => x.Allocations).HasForeignKey(x => x.BagId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ReferenceType, x.ReferenceId });
        // MySQL không hỗ trợ filtered unique index. Dùng generated column: allocation
        // ACTIVE giữ BagId, các trạng thái lịch sử trả NULL (unique index cho phép nhiều NULL).
        builder.Property<int?>("ActiveBagId")
            .HasComputedColumnSql(
                "CASE WHEN `Status` = 'ACTIVE' AND `IsDeleted` = 0 THEN `BagId` ELSE NULL END",
                stored: true);
        builder.HasIndex(x => x.BagId);
        builder.HasIndex("ActiveBagId").IsUnique();
    }
}
