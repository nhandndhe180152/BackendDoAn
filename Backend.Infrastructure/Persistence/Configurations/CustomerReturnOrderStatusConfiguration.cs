using System;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.Configurations;

public class CustomerReturnOrderStatusConfiguration : IEntityTypeConfiguration<CustomerReturnOrderStatus>
{
    public void Configure(EntityTypeBuilder<CustomerReturnOrderStatus> builder)
    {
        builder.ToTable(TableNames.CustomerReturnOrderStatus);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Name).HasMaxLength(100);
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Color).HasMaxLength(30);

        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName("UX_CustomerReturnOrderStatus_Code");
    }
}
