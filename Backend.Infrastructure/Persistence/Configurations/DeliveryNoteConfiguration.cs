using System;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.Configurations;

public class DeliveryNoteConfiguration : IEntityTypeConfiguration<DeliveryNote>
{
    public void Configure(EntityTypeBuilder<DeliveryNote> builder)
    {
        builder.ToTable(TableNames.DeliveryNote);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.TrackingCode).HasMaxLength(255);
        builder.Property(x => x.CarrierName).HasMaxLength(255);
        builder.Property(x => x.SenderName).HasMaxLength(255);
        builder.Property(x => x.SenderPhone).HasMaxLength(50);
        builder.Property(x => x.SenderAddress).HasMaxLength(500);
        builder.Property(x => x.ReceiverName).HasMaxLength(255);
        builder.Property(x => x.ReceiverPhone).HasMaxLength(50);
        builder.Property(x => x.ReceiverAddress).HasMaxLength(500);
        builder.Property(x => x.DeclaredWeight).HasColumnType("decimal(18,3)");
        builder.Property(x => x.CODAmount).HasColumnType("decimal(18,2)");

        // Quan hệ 1-1 được sở hữu bởi InboundOrder.DeliveryNoteId — không định nghĩa thêm FK ở đây
    }
}
