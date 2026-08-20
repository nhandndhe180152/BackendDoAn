using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class StockTransferBagConfiguration : IEntityTypeConfiguration<StockTransferBag>
{
    public void Configure(EntityTypeBuilder<StockTransferBag> builder)
    {
        builder.ToTable(TableNames.StockTransferBag);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.WeightKg).HasColumnType("decimal(18,3)").IsRequired();
        builder.Property(x => x.MoisturePercent).HasColumnType("decimal(5,2)");
        builder.Property(x => x.ImpurityPercent).HasColumnType("decimal(5,2)");
        builder.Property(x => x.MoldLevel).HasMaxLength(50);
        builder.Property(x => x.PestLevel).HasMaxLength(50);
        builder.Property(x => x.PackagingStatus).HasMaxLength(50);
        builder.Property(x => x.QualityResult).HasMaxLength(30);
        builder.Property(x => x.QualityNote).HasMaxLength(1000);
        builder.Property(x => x.Disposition).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(500);

        builder.HasOne(x => x.StockTransferItem)
            .WithMany(x => x.Bags)
            .HasForeignKey(x => x.StockTransferItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Bag)
            .WithMany()
            .HasForeignKey(x => x.BagId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SourceLot)
            .WithMany()
            .HasForeignKey(x => x.SourceLotId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.TargetLot)
            .WithMany()
            .HasForeignKey(x => x.TargetLotId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.QuarantineLocation)
            .WithMany()
            .HasForeignKey(x => x.QuarantineLocationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.StockTransferItemId).HasDatabaseName("IX_StockTransferBag_StockTransferItemId");
        builder.HasIndex(x => x.BagId).HasDatabaseName("IX_StockTransferBag_BagId");
        builder.HasIndex(x => x.TargetLotId).HasDatabaseName("IX_StockTransferBag_TargetLotId");
        builder.HasIndex(x => x.QuarantineLocationId).HasDatabaseName("IX_StockTransferBag_QuarantineLocationId");
    }
}
