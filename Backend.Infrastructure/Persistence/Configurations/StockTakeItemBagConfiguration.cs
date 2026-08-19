using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class StockTakeItemBagConfiguration : IEntityTypeConfiguration<StockTakeItemBag>
{
    public void Configure(EntityTypeBuilder<StockTakeItemBag> builder)
    {
        builder.ToTable(TableNames.StockTakeItemBag);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.QrCode).HasMaxLength(100);
        builder.Property(x => x.SystemWeightKg).HasColumnType("decimal(18,3)");
        builder.Property(x => x.CountedWeightKg).HasColumnType("decimal(18,3)");
        builder.Property(x => x.MoisturePercent).HasColumnType("decimal(5,2)");
        builder.Property(x => x.ImpurityPercent).HasColumnType("decimal(5,2)");
        builder.Property(x => x.QualityResult).HasMaxLength(30);
        builder.Property(x => x.MoldLevel).HasMaxLength(50);
        builder.Property(x => x.PestLevel).HasMaxLength(50);
        builder.Property(x => x.PackagingStatus).HasMaxLength(50);
        builder.Property(x => x.QualityNote).HasMaxLength(1000);
        builder.Property(x => x.Disposition).HasMaxLength(30).IsRequired();
        builder.Property(x => x.DispositionNote).HasMaxLength(500);

        builder.Ignore(x => x.EffectiveWeightKg);
        builder.Ignore(x => x.StaysAtLocation);
        builder.Ignore(x => x.LeavesLocation);

        builder.HasOne(x => x.StockTakeItem)
               .WithMany(x => x.Bags)
               .HasForeignKey(x => x.StockTakeItemId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.PaddyLotBag)
               .WithMany()
               .HasForeignKey(x => x.PaddyLotBagId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.TargetLocation)
               .WithMany()
               .HasForeignKey(x => x.TargetLocationId)
               .OnDelete(DeleteBehavior.NoAction)
               .IsRequired(false);

        // Chặn quét trùng một bao trong cùng một dòng kiểm kê ngay ở tầng DB.
        builder.HasIndex(x => new { x.StockTakeItemId, x.PaddyLotBagId })
               .IsUnique()
               .HasDatabaseName("UX_StockTakeItemBag_Item_Bag");
    }
}
