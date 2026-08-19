using System;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.Configurations;

public class StockTakeItemConfiguration : IEntityTypeConfiguration<StockTakeItem>
{
    public void Configure(EntityTypeBuilder<StockTakeItem> builder)
    {
        builder.ToTable(TableNames.StockTakeItem);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.SystemQuantity).HasColumnType("decimal(18,3)");
        builder.Property(x => x.ActualQuantity).HasColumnType("decimal(18,3)");
        builder.Property(x => x.AdjustedWeightKg).HasColumnType("decimal(18,3)");
        builder.Property(x => x.VarianceReason).HasMaxLength(500);

        // Computed props — không ánh xạ xuống DB
        builder.Ignore(x => x.Difference);
        builder.Ignore(x => x.VariancePercent);
        builder.Ignore(x => x.VarianceSeverity);
        builder.Ignore(x => x.AbsoluteVarianceKg);
        builder.Ignore(x => x.BagDifference);
        builder.Ignore(x => x.HasBagVariance);

        // FK: PaddyLotId → PaddyLot (nullable, sản phẩm không theo lô để null)
        builder.HasOne(x => x.PaddyLot)
               .WithMany()
               .HasForeignKey(x => x.PaddyLotId)
               .OnDelete(DeleteBehavior.NoAction)
               .IsRequired(false);

        // FK: RecountConfirmedBy → User (nullable)
        builder.HasOne(x => x.RecountConfirmedByUser)
               .WithMany()
               .HasForeignKey(x => x.RecountConfirmedBy)
               .OnDelete(DeleteBehavior.NoAction)
               .IsRequired(false);
    }
}
