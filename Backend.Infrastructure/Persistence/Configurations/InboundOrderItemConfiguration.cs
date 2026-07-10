using System;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.Configurations;

public class InboundOrderItemConfiguration : IEntityTypeConfiguration<InboundOrderItem>
{
    public void Configure(EntityTypeBuilder<InboundOrderItem> builder)
    {
        builder.ToTable(TableNames.InboundOrderItem);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.UnitCostPrice)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.QuantityOrdered)
            .HasColumnType("decimal(18,3)");

        builder.Property(x => x.QuantityReceived)
            .HasColumnType("decimal(18,3)");

        builder.Property(x => x.ExpectedWeightKg)
            .HasColumnType("decimal(18,4)");

        builder.Property(x => x.ActualWeightKg)
            .HasColumnType("decimal(18,4)");

        builder.HasOne(x => x.InboundOrder)
            .WithMany(x => x.InboundOrderItems)
            .HasForeignKey(x => x.InboundOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ProductVariant)
            .WithMany()
            .HasForeignKey(x => x.ProductVariantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PurchaseOrderItem)
            .WithMany()
            .HasForeignKey(x => x.PurchaseOrderItemId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.PaddyLot)
            .WithMany()
            .HasForeignKey(x => x.PaddyLotId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.InboundOrderId).HasDatabaseName("IX_InboundOrderItem_InboundOrderId");
        builder.HasIndex(x => x.PaddyLotId).HasDatabaseName("IX_InboundOrderItem_PaddyLotId");
    }
}
