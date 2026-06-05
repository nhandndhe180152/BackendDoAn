using System;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.Configurations;

public class PurchaseOrderItemConfiguration : IEntityTypeConfiguration<PurchaseOrderItem>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderItem> builder)
    {
        builder.ToTable(TableNames.PurchaseOrderItem);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.UnitCostPrice)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.ExpectedWeightKg)
            .HasColumnType("decimal(18,4)");

        builder.Property(x => x.ActualWeightKg)
            .HasColumnType("decimal(18,4)");
    }
}
