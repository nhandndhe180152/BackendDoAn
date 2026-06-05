using System;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.Configurations;

public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable(TableNames.ProductVariant);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.CostPrice)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.SalePrice)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.Weight)
            .HasColumnType("decimal(18,4)");

        builder.Property(x => x.MinStockLevel)
    .HasPrecision(18, 2)
    .IsRequired(false);

        builder.HasIndex(x => x.MinStockLevel)
            .HasDatabaseName("IX_ProductVariant_MinStockLevel");
    }
}
