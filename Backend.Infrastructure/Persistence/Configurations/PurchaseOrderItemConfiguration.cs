using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class PurchaseOrderItemConfiguration : IEntityTypeConfiguration<PurchaseOrderItem>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderItem> builder)
    {
        builder.ToTable(TableNames.PurchaseOrderItem);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.QuantityOrdered).HasColumnType("decimal(18,3)").IsRequired();
        builder.Property(x => x.UnitCostPrice).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.LineAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.Note).HasMaxLength(500);

        builder.HasOne(x => x.PurchaseOrder)
            .WithMany(x => x.PurchaseOrderItems)
            .HasForeignKey(x => x.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ProductVariant)
            .WithMany()
            .HasForeignKey(x => x.ProductVariantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.PurchaseOrderId).HasDatabaseName("IX_PurchaseOrderItem_PurchaseOrderId");
    }
}
