using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class PaddyPurchaseReceiptConfiguration : IEntityTypeConfiguration<PaddyPurchaseReceipt>
{
    public void Configure(EntityTypeBuilder<PaddyPurchaseReceipt> builder)
    {
        builder.ToTable(TableNames.PaddyPurchaseReceipt);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.ReceiptCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ActualWeightKg).HasColumnType("decimal(18,3)").IsRequired();
        builder.Property(x => x.AgreedPrice).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.TotalAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.PaidAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.DebtAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.QualityJson).HasColumnType("json");
        builder.Property(x => x.BagDetailsJson).HasColumnType("json");
        builder.Property(x => x.PriceAdjustReason).HasMaxLength(500);

        builder.HasIndex(x => new { x.OrganizationId, x.ReceiptCode })
            .IsUnique()
            .HasDatabaseName("UX_PaddyPurchaseReceipt_OrgId_Code");

        builder.HasOne(x => x.Organization)
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Schedule)
            .WithMany(x => x.PaddyPurchaseReceipts)
            .HasForeignKey(x => x.ScheduleId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Farmer)
            .WithMany(x => x.PaddyPurchaseReceipts)
            .HasForeignKey(x => x.FarmerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RiceVariety)
            .WithMany()
            .HasForeignKey(x => x.RiceVarietyId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Warehouse)
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.FarmerId).HasDatabaseName("IX_PaddyPurchaseReceipt_FarmerId");
        builder.HasIndex(x => x.ReceiptDate).HasDatabaseName("IX_PaddyPurchaseReceipt_ReceiptDate");
    }
}
