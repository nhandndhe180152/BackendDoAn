using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class PutawayDecisionConfiguration : IEntityTypeConfiguration<PutawayDecision>
{
    public void Configure(EntityTypeBuilder<PutawayDecision> builder)
    {
        builder.ToTable(TableNames.PutawayDecision);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.RequiredWeightKg).HasColumnType("decimal(18,3)");
        builder.Property(x => x.SuggestedScore).HasColumnType("decimal(8,6)");
        builder.Property(x => x.ReferenceType).HasMaxLength(50);
        builder.Property(x => x.OverrideReason).HasMaxLength(500);

        builder.HasOne(x => x.Warehouse)
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProductVariant)
            .WithMany()
            .HasForeignKey(x => x.ProductVariantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PaddyLot)
            .WithMany()
            .HasForeignKey(x => x.PaddyLotId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.SuggestedLocation)
            .WithMany()
            .HasForeignKey(x => x.SuggestedLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SelectedLocation)
            .WithMany()
            .HasForeignKey(x => x.SelectedLocationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
