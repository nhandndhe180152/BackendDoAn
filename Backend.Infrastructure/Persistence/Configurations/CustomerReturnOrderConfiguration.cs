using System;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.Configurations;

public class CustomerReturnOrderConfiguration : IEntityTypeConfiguration<CustomerReturnOrder>
{
    public void Configure(EntityTypeBuilder<CustomerReturnOrder> builder)
    {
        builder.ToTable(TableNames.CustomerReturnOrder);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.ReturnCode).HasMaxLength(50);
        builder.Property(x => x.ReturnReason).HasMaxLength(500);
        builder.Property(x => x.Note).HasMaxLength(1000);

        builder.HasIndex(x => x.ReturnCode)
            .IsUnique()
            .HasDatabaseName("UX_CustomerReturnOrder_ReturnCode");

        builder.HasOne(x => x.Warehouse)
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CustomerReturnOrderStatus)
            .WithMany()
            .HasForeignKey(x => x.CustomerReturnOrderStatusId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.OutboundOrder)
            .WithMany()
            .HasForeignKey(x => x.OutboundOrderId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.ApprovedByUser)
            .WithMany()
            .HasForeignKey(x => x.ApprovedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
