using System;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.Configurations;

public class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable(TableNames.Location);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.ZoneName).HasMaxLength(255);
        builder.Property(x => x.ShelfRow).HasMaxLength(100);
        builder.Property(x => x.ShelfLevel).HasMaxLength(100);
        builder.Property(x => x.SlotCode).HasMaxLength(100);
        builder.Property(x => x.Description).HasMaxLength(500);

        builder.Property(x => x.MaxCapacity).HasColumnType("decimal(18,3)");
        builder.Property(x => x.CurrentOccupancy).HasColumnType("decimal(18,3)").HasDefaultValue(0.000m);

        // Chỉ cấu hình FK AllowedCategory (SET NULL) — Warehouse FK được xử lý qua convention
        builder.HasOne(x => x.AllowedCategory)
            .WithMany()
            .HasForeignKey(x => x.AllowedCategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.CurrentProductVariant)
            .WithMany()
            .HasForeignKey(x => x.CurrentProductVariantId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.AllowedCategoryId)
            .HasDatabaseName("IX_Location_AllowedCategoryId");

        builder.HasIndex(x => x.IsQuarantine)
            .HasDatabaseName("IX_Location_IsQuarantine");

        builder.HasIndex(x => new { x.WarehouseId, x.IsActive, x.IsDeleted, x.IsQuarantine, x.CurrentProductVariantId })
            .HasDatabaseName("IX_Location_PutawayCandidate");
    }
}
