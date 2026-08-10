using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class CustomerReturnOrderItemAllocationConfiguration : IEntityTypeConfiguration<CustomerReturnOrderItemAllocation>
{
    public void Configure(EntityTypeBuilder<CustomerReturnOrderItemAllocation> builder)
    {
        builder.ToTable(TableNames.CustomerReturnOrderItemAllocation);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.QuantityReturned).HasColumnType("decimal(18,3)").IsRequired();
        builder.Property(x => x.QuantityGood).HasColumnType("decimal(18,3)").IsRequired();
        builder.Property(x => x.QuantityDamaged).HasColumnType("decimal(18,3)").IsRequired();
        builder.Property(x => x.QuantityRejected).HasColumnType("decimal(18,3)").IsRequired();
        builder.Property(x => x.CreditQuantity).HasColumnType("decimal(18,3)").IsRequired();

        builder.Property(x => x.UnitCreditPrice).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.CreditAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.Property(x => x.BagDetailsJson).HasColumnType("longtext");

        builder.HasOne(x => x.CustomerReturnOrderItem)
            .WithMany(x => x.Allocations)
            .HasForeignKey(x => x.CustomerReturnOrderItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.OutboundOrderItemAllocation)
            .WithMany()
            .HasForeignKey(x => x.OutboundOrderItemAllocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PaddyLot)
            .WithMany()
            .HasForeignKey(x => x.PaddyLotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProductVariant)
            .WithMany()
            .HasForeignKey(x => x.ProductVariantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.OriginalLocation)
            .WithMany()
            .HasForeignKey(x => x.OriginalLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RestockLocation)
            .WithMany()
            .HasForeignKey(x => x.RestockLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.QuarantineLocation)
            .WithMany()
            .HasForeignKey(x => x.QuarantineLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.CustomerReturnOrderItemId).HasDatabaseName("IX_CustomerReturnAllocation_ReturnItem");
        builder.HasIndex(x => x.OutboundOrderItemAllocationId).HasDatabaseName("IX_CustomerReturnAllocation_OutboundAllocation");
        builder.HasIndex(x => x.PaddyLotId).HasDatabaseName("IX_CustomerReturnAllocation_PaddyLot");
    }
}
