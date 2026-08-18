using System;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.Configurations;

public class ReturnToSupplierOrderItemConfiguration : IEntityTypeConfiguration<ReturnToSupplierOrderItem>
{
    public void Configure(EntityTypeBuilder<ReturnToSupplierOrderItem> builder)
    {
        builder.ToTable(TableNames.ReturnToSupplierOrderItem);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.DamageReason).HasMaxLength(500);
        builder.Property(x => x.Note).HasMaxLength(1000);

        builder.HasOne(x => x.ReturnToSupplierOrder)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.ReturnToSupplierOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ProductVariant)
            .WithMany()
            .HasForeignKey(x => x.ProductVariantId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.QuarantineLocation)
            .WithMany()
            .HasForeignKey(x => x.QuarantineLocationId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
