using System;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.Configurations;

public class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.ToTable(TableNames.Alert);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.AlertType).HasMaxLength(50);
        builder.Property(x => x.Severity).HasMaxLength(20);
        builder.Property(x => x.Message).HasMaxLength(1000);
        builder.Property(x => x.RelatedEntityType).HasMaxLength(100);
        builder.Property(x => x.Status).HasMaxLength(30);

        builder.HasOne(x => x.Warehouse)
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProductVariant)
            .WithMany()
            .HasForeignKey(x => x.ProductVariantId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Location)
            .WithMany()
            .HasForeignKey(x => x.LocationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.AcknowledgedByUser)
            .WithMany()
            .HasForeignKey(x => x.AcknowledgedBy)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(x => x.DeduplicationKey)
            .HasMaxLength(200);

        builder.HasIndex(x => x.DeduplicationKey)
            .IsUnique()
            .HasDatabaseName("UX_Alert_DeduplicationKey");

        builder.HasIndex(x => new { x.WarehouseId, x.Status })
            .HasDatabaseName("IX_Alert_WarehouseId_Status");

        builder.HasIndex(x => x.AlertType)
            .HasDatabaseName("IX_Alert_AlertType");

        builder.HasIndex(x => new { x.AlertType, x.WarehouseId, x.ProductVariantId, x.Status, x.IsDeleted })
            .HasDatabaseName("IX_Alert_ActiveLowStockLookup");
    }
}
