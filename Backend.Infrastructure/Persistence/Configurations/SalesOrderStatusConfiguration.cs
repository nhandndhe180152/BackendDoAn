using System;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.Configurations;

public class SalesOrderStatusConfiguration : IEntityTypeConfiguration<SalesOrderStatus>
{
    public void Configure(EntityTypeBuilder<SalesOrderStatus> builder)
    {
        builder.ToTable(TableNames.SalesOrderStatus);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();
    }
}
