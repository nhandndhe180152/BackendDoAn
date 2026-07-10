using System;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.Configurations;

public class OutboundOrderItemConfiguration : IEntityTypeConfiguration<OutboundOrderItem>
{
    public void Configure(EntityTypeBuilder<OutboundOrderItem> builder)
    {
        builder.ToTable(TableNames.OutboundOrderItem);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.UnitCostPrice)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.QuantityOrdered)
            .HasColumnType("decimal(18,3)");

        builder.Property(x => x.QuantityPicked)
            .HasColumnType("decimal(18,3)");

        builder.Property(x => x.ExpectedWeightKg)
            .HasColumnType("decimal(18,4)");

        builder.Property(x => x.ActualWeightKg)
            .HasColumnType("decimal(18,4)");

        builder.HasOne(x => x.OutboundOrder)
            .WithMany(x => x.OutboundOrderItems)
            .HasForeignKey(x => x.OutboundOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ProductVariant)
            .WithMany()
            .HasForeignKey(x => x.ProductVariantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SalesOrderItem)
            .WithMany()
            .HasForeignKey(x => x.SalesOrderItemId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.OutboundOrderId).HasDatabaseName("IX_OutboundOrderItem_OutboundOrderId");
        builder.HasIndex(x => x.SalesOrderItemId).HasDatabaseName("IX_OutboundOrderItem_SalesOrderItemId");
    }
}
