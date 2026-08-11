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
        builder.HasIndex(x => x.BagId).HasFilter("[Status] = 'ACTIVE'").IsUnique();
    }
}
