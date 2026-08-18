using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class PaddyLotBagMovementConfiguration : IEntityTypeConfiguration<PaddyLotBagMovement>
{
    public void Configure(EntityTypeBuilder<PaddyLotBagMovement> builder)
    {
        builder.ToTable(TableNames.PaddyLotBagMovement);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MovementType).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ReferenceType).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.Property(x => x.WeightKg).HasPrecision(18, 3);
        builder.Property(x => x.BeforeWeightKg).HasPrecision(18, 3);
        builder.Property(x => x.AfterWeightKg).HasPrecision(18, 3);
        builder.HasIndex(x => new { x.BagId, x.CreatedDate });
        builder.HasIndex(x => new { x.ReferenceType, x.ReferenceId });
        builder.HasOne(x => x.Bag).WithMany(x => x.Movements).HasForeignKey(x => x.BagId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.FromLocation).WithMany().HasForeignKey(x => x.FromLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ToLocation).WithMany().HasForeignKey(x => x.ToLocationId).OnDelete(DeleteBehavior.Restrict);
    }
}
