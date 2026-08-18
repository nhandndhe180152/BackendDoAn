using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class OutboundOrderItemAllocationConfiguration : IEntityTypeConfiguration<OutboundOrderItemAllocation>
{
    public void Configure(EntityTypeBuilder<OutboundOrderItemAllocation> builder)
    {
        builder.ToTable(TableNames.OutboundOrderItemAllocation);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.QuantityAllocated)
            .HasColumnType("decimal(18,3)")
            .IsRequired();

        builder.Property(x => x.QuantityPicked)
            .HasColumnType("decimal(18,3)")
            .IsRequired();

        builder.Property(x => x.UnitCostPrice)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(x => x.RowVersion)
            .IsRowVersion()
            .IsRequired(false);

        // FK → OutboundOrderItem (cascade delete)
        builder.HasOne(x => x.OutboundOrderItem)
            .WithMany(x => x.Allocations)
            .HasForeignKey(x => x.OutboundOrderItemId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK → Inventory
        builder.HasOne(x => x.Inventory)
            .WithMany()
            .HasForeignKey(x => x.InventoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK → PaddyLot (nullable)
        builder.HasOne(x => x.PaddyLot)
            .WithMany()
            .HasForeignKey(x => x.PaddyLotId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        // FK → Location
        builder.HasOne(x => x.Location)
            .WithMany()
            .HasForeignKey(x => x.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.OutboundOrderItemId)
            .HasDatabaseName("IX_OutboundOrderItemAllocation_OutboundOrderItemId");

        builder.HasIndex(x => x.InventoryId)
            .HasDatabaseName("IX_OutboundOrderItemAllocation_InventoryId");

        builder.HasIndex(x => x.PaddyLotId)
            .HasDatabaseName("IX_OutboundOrderItemAllocation_PaddyLotId");
    }
}
