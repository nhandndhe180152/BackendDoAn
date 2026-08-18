using System;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.Configurations;

public class OutboundOrderStatusConfiguration : IEntityTypeConfiguration<OutboundOrderStatus>
{
    public void Configure(EntityTypeBuilder<OutboundOrderStatus> builder)
    {
        builder.ToTable(TableNames.OutboundOrderStatus);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();
        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(100);
        builder.HasIndex(x => x.Code)
            .IsUnique();
    }
}
