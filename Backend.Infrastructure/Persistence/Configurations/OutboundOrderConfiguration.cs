using System;
using Backend.Application.Constants;
using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class OutboundOrderConfiguration : IEntityTypeConfiguration<OutboundOrder>
{
    public void Configure(EntityTypeBuilder<OutboundOrder> builder)
    {
        builder.ToTable(TableNames.OutboundOrder);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.TotalDispatchedValue)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.TotalDispatchedSaleValue)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.Note).HasMaxLength(OutboundOrderConstants.NoteMaxLength);
        builder.Property(x => x.CancelReason).HasMaxLength(500);
        builder.Property(x => x.PackingScaleDevice).HasMaxLength(255);
        builder.Property(x => x.ReceiverName).HasMaxLength(255);
        builder.Property(x => x.DeliveryNote).HasMaxLength(1000);
        builder.Property(x => x.ProofImageUrl).HasMaxLength(1000);

        // FK → SalesOrder (NOT NULL: mỗi phiếu xuất phải thuộc đơn bán)
        builder.HasOne(x => x.SalesOrder)
            .WithMany(x => x.OutboundOrders)
            .HasForeignKey(x => x.SalesOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Organization)
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Warehouse)
            .WithMany(w => w.OutboundOrders)
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.OutboundOrderStatus)
            .WithMany(s => s.OutboundOrders)
            .HasForeignKey(x => x.OutboundOrderStatusId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AssignedUser)
            .WithMany()
            .HasForeignKey(x => x.AssignedUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.SalesOrderId).HasDatabaseName("IX_OutboundOrder_SalesOrderId");
        builder.HasIndex(x => x.OrganizationId).HasDatabaseName("IX_OutboundOrder_OrganizationId");
    }
}

