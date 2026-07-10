using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class MillingOrderOutputConfiguration : IEntityTypeConfiguration<MillingOrderOutput>
{
    public void Configure(EntityTypeBuilder<MillingOrderOutput> builder)
    {
        builder.ToTable(TableNames.MillingOrderOutput);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.OutputType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.OutputWeightKg).HasColumnType("decimal(18,3)").IsRequired();
        builder.Property(x => x.UnitCost).HasColumnType("decimal(18,2)");
        builder.Property(x => x.IsByproduct).HasDefaultValue(false);

        builder.HasOne(x => x.MillingOrder)
            .WithMany(x => x.MillingOrderOutputs)
            .HasForeignKey(x => x.MillingOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ProductVariant)
            .WithMany()
            .HasForeignKey(x => x.ProductVariantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.OutputLot)
            .WithMany()
            .HasForeignKey(x => x.OutputLotId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.MillingOrderId).HasDatabaseName("IX_MillingOrderOutput_MillingOrderId");
    }
}
