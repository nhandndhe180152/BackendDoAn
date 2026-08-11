using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class PaddyLotBagConfiguration : IEntityTypeConfiguration<PaddyLotBag>
{
    public void Configure(EntityTypeBuilder<PaddyLotBag> builder)
    {
        builder.ToTable(TableNames.PaddyLotBag);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.WeightKg).HasColumnType("decimal(18,3)").IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.QrCode).HasMaxLength(100);
        builder.Property(x => x.StandardWeightKg).HasColumnType("decimal(18,3)");
        builder.Property(x => x.BagKind).HasMaxLength(20).IsRequired();
        builder.Property(x => x.OpenBagKey).HasMaxLength(100);
        builder.HasIndex(x => x.OpenBagKey).IsUnique();
        builder.HasIndex(x => new { x.LotId, x.BagNo }).IsUnique();
        builder.HasIndex(x => x.LocationId);
        builder.HasIndex(x => x.QrCode).IsUnique();
        builder.HasOne(x => x.Lot).WithMany(x => x.Bags).HasForeignKey(x => x.LotId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.SetNull);
    }
}
