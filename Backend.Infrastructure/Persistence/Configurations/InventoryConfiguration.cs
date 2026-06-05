using System;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.Configurations;

public class InventoryConfiguration : IEntityTypeConfiguration<Inventory>
{
    public void Configure(EntityTypeBuilder<Inventory> builder)
    {
        builder.ToTable(TableNames.Inventory);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.CostPrice)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.QuantityOnHand)
            .IsRequired();

        builder.Property(x => x.QuantityReserved)
            .HasDefaultValue(0);

        builder.HasOne(x => x.Warehouse)
            .WithMany(x => x.Inventories)
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Location)
            .WithMany()
            .HasForeignKey(x => x.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProductVariant)
            .WithMany()
            .HasForeignKey(x => x.ProductVariantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.InboundOrder)
            .WithMany()
            .HasForeignKey(x => x.InboundOrderId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.LocationId)
            .HasDatabaseName("IX_Inventory_LocationId");

        builder.HasIndex(x => x.WarehouseId)
            .HasDatabaseName("IX_Inventory_WarehouseId");

        builder.HasIndex(x => x.ProductVariantId)
            .HasDatabaseName("IX_Inventory_ProductVariantId");

        builder.HasIndex(x => new { x.ProductVariantId, x.WarehouseId, x.LocationId })
            .IsUnique()
            .HasDatabaseName("UX_Inventory_ProductVariant_Warehouse_Location");
    }
}
