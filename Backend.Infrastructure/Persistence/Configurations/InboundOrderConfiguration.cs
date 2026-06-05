using System;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.Configurations;

public class InboundOrderConfiguration : IEntityTypeConfiguration<InboundOrder>
{
    public void Configure(EntityTypeBuilder<InboundOrder> builder)
    {
        builder.ToTable(TableNames.InboundOrder);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.TotalAssetValue)
            .HasColumnType("decimal(18,2)");

        builder.HasOne(x => x.DeliveryNote)
            .WithOne(x => x.InboundOrder)
            .HasForeignKey<InboundOrder>(x => x.DeliveryNoteId)
            .IsRequired(false);
    }
}
