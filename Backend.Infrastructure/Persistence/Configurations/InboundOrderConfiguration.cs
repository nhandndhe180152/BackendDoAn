using System;
using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class InboundOrderConfiguration : IEntityTypeConfiguration<InboundOrder>
{
    public void Configure(EntityTypeBuilder<InboundOrder> builder)
    {
        builder.ToTable(TableNames.InboundOrder);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        // POCode là nullable fallback — chỉ dùng khi không có PurchaseOrderId
        builder.Property(x => x.POCode).HasMaxLength(50).IsRequired(false);
        builder.Property(x => x.SourceType).HasMaxLength(20).IsRequired(false);
        builder.Property(x => x.TotalAssetValue)
            .HasColumnType("decimal(18,2)");

        builder.HasIndex(x => x.POCode)
            .HasDatabaseName("IX_InboundOrder_POCode");

        // 1-1 DeliveryNote — InboundOrder sở hữu FK
        builder.HasOne(x => x.DeliveryNote)
            .WithOne(x => x.InboundOrder)
            .HasForeignKey<InboundOrder>(x => x.DeliveryNoteId)
            .IsRequired(false);

        builder.HasOne(x => x.PurchaseOrder)
            .WithMany(x => x.InboundOrders)
            .HasForeignKey(x => x.PurchaseOrderId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.PaddyPurchaseReceipt)
            .WithMany()
            .HasForeignKey(x => x.PaddyPurchaseReceiptId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Organization)
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.StockTransfer)
            .WithMany()
            .HasForeignKey(x => x.StockTransferId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.PurchaseOrderId).HasDatabaseName("IX_InboundOrder_PurchaseOrderId");
        builder.HasIndex(x => x.PaddyPurchaseReceiptId).HasDatabaseName("IX_InboundOrder_PaddyPurchaseReceiptId");
        builder.HasIndex(x => x.OrganizationId).HasDatabaseName("IX_InboundOrder_OrganizationId");
        builder.HasIndex(x => x.StockTransferId).HasDatabaseName("IX_InboundOrder_StockTransferId");
    }
}

