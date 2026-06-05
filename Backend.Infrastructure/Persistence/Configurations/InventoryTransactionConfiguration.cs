using System;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.Configurations;

public class InventoryTransactionConfiguration : IEntityTypeConfiguration<InventoryTransaction>
{
    public void Configure(EntityTypeBuilder<InventoryTransaction> builder)
    {
        builder.ToTable(TableNames.InventoryTransaction);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.TransactionType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.ReferenceType)
            .HasMaxLength(50);

        builder.Property(x => x.WeightKg)
            .HasColumnType("decimal(18,3)");

        builder.Property(x => x.Note)
            .HasMaxLength(500);

        builder.HasOne(x => x.Inventory)
            .WithMany(x => x.InventoryTransactions)
            .HasForeignKey(x => x.InventoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Warehouse)
            .WithMany(x => x.InventoryTransactions)
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

        builder.HasOne(x => x.IotWeightLog)
            .WithMany()
            .HasForeignKey(x => x.IotWeightLogId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.InventoryId)
            .HasDatabaseName("IX_InventoryTransaction_InventoryId");

        builder.HasIndex(x => new { x.ReferenceType, x.ReferenceId, x.ReferenceItemId })
            .HasDatabaseName("IX_InventoryTransaction_Reference");

        builder.HasIndex(x => new { x.ProductVariantId, x.CreatedDate })
            .HasDatabaseName("IX_InventoryTransaction_ProductVariant_CreatedDate");

        builder.HasIndex(x => x.IotWeightLogId)
            .HasDatabaseName("IX_InventoryTransaction_IotWeightLogId");
    }
}
