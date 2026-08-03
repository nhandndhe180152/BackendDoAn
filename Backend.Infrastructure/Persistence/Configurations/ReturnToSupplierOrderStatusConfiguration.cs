using System;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.Configurations;

public class ReturnToSupplierOrderStatusConfiguration : IEntityTypeConfiguration<ReturnToSupplierOrderStatus>
{
    public void Configure(EntityTypeBuilder<ReturnToSupplierOrderStatus> builder)
    {
        builder.ToTable(TableNames.ReturnToSupplierOrderStatus);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Name).HasMaxLength(100);
        builder.Property(x => x.Color).HasMaxLength(30);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(100);
        builder.HasIndex(x => x.Code).IsUnique();
    }
}
