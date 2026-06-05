using System;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.Configurations;

public class PurchaseOrderStatusConfiguration : IEntityTypeConfiguration<PurchaseOrderStatus>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderStatus> builder)
    {
        builder.ToTable(TableNames.PurchaseOrderStatus);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();
    }
}
