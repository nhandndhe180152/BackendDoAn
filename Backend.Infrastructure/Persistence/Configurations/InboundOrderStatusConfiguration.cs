using System;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.Configurations;

public class InboundOrderStatusConfiguration : IEntityTypeConfiguration<InboundOrderStatus>
{
    public void Configure(EntityTypeBuilder<InboundOrderStatus> builder)
    {
        builder.ToTable(TableNames.InboundOrderStatus);
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
