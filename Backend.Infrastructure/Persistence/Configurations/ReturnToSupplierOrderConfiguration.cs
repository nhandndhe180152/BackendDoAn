using System;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.Configurations;

public class ReturnToSupplierOrderConfiguration : IEntityTypeConfiguration<ReturnToSupplierOrder>
{
    public void Configure(EntityTypeBuilder<ReturnToSupplierOrder> builder)
    {
        builder.ToTable(TableNames.ReturnToSupplierOrder);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.ReturnCode).HasMaxLength(50);
        builder.Property(x => x.Note).HasMaxLength(1000);

        builder.HasIndex(x => x.ReturnCode)
            .IsUnique()
            .HasDatabaseName("UX_ReturnToSupplierOrder_ReturnCode");

        builder.HasOne(x => x.Warehouse)
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Supplier)
            .WithMany()
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ReturnToSupplierOrderStatus)
            .WithMany()
            .HasForeignKey(x => x.ReturnToSupplierOrderStatusId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.InboundOrder)
            .WithMany()
            .HasForeignKey(x => x.InboundOrderId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.ApprovedByUser)
            .WithMany()
            .HasForeignKey(x => x.ApprovedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
