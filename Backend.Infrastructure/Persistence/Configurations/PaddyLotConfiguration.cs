using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class PaddyLotConfiguration : IEntityTypeConfiguration<PaddyLot>
{
    public void Configure(EntityTypeBuilder<PaddyLot> builder)
    {
        builder.ToTable(TableNames.PaddyLot);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.LotCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.LotType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.InitialWeightKg).HasColumnType("decimal(18,3)").IsRequired();
        builder.Property(x => x.RemainingWeightKg).HasColumnType("decimal(18,3)").IsRequired();
        builder.Property(x => x.CostPricePerKg).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.QualityStatus).HasMaxLength(100);
        builder.Property(x => x.QrCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.QrImageUrl).HasMaxLength(500);
        builder.HasIndex(x => x.QrCode).IsUnique().HasDatabaseName("UX_PaddyLot_QrCode");

        builder.HasIndex(x => new { x.OrganizationId, x.LotCode })
            .IsUnique()
            .HasDatabaseName("UX_PaddyLot_OrgId_Code");

        builder.HasOne(x => x.Organization)
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProductVariant)
            .WithMany()
            .HasForeignKey(x => x.ProductVariantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RiceVariety)
            .WithMany(x => x.PaddyLots)
            .HasForeignKey(x => x.RiceVarietyId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Status)
            .WithMany()
            .HasForeignKey(x => x.StatusId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SourceReceipt)
            .WithOne(x => x.PaddyLot)
            .HasForeignKey<PaddyLot>(x => x.SourceReceiptId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.SourceMillingOrder)
            .WithMany()
            .HasForeignKey(x => x.SourceMillingOrderId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Warehouse)
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Location)
            .WithMany()
            .HasForeignKey(x => x.LocationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.ParentLot)
            .WithMany()
            .HasForeignKey(x => x.ParentLotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.WarehouseId).HasDatabaseName("IX_PaddyLot_WarehouseId");
        builder.HasIndex(x => x.StatusId).HasDatabaseName("IX_PaddyLot_StatusId");
        builder.HasIndex(x => x.InboundDate).HasDatabaseName("IX_PaddyLot_InboundDate");
    }
}
