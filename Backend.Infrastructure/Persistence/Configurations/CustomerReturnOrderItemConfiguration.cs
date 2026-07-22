using System;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.Configurations;

public class CustomerReturnOrderItemConfiguration : IEntityTypeConfiguration<CustomerReturnOrderItem>
{
    public void Configure(EntityTypeBuilder<CustomerReturnOrderItem> builder)
    {
        builder.ToTable(TableNames.CustomerReturnOrderItem);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.QualityStatus).HasMaxLength(30);
        builder.Property(x => x.DamageReason).HasMaxLength(500);
        builder.Property(x => x.Note).HasMaxLength(1000);

        builder.Property(x => x.QuantityReturned).HasColumnType("decimal(18,3)").IsRequired();
        builder.Property(x => x.QuantityGood).HasColumnType("decimal(18,3)").IsRequired();
        builder.Property(x => x.QuantityDamaged).HasColumnType("decimal(18,3)").IsRequired();

        builder.HasOne(x => x.CustomerReturnOrder)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.CustomerReturnOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ProductVariant)
            .WithMany()
            .HasForeignKey(x => x.ProductVariantId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.RestockLocation)
            .WithMany()
            .HasForeignKey(x => x.RestockLocationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.QuarantineLocation)
            .WithMany()
            .HasForeignKey(x => x.QuarantineLocationId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
