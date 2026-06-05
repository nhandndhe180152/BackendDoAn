using System;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.Configurations;

public class StockTakeItemConfiguration : IEntityTypeConfiguration<StockTakeItem>
{
    public void Configure(EntityTypeBuilder<StockTakeItem> builder)
    {
        builder.ToTable(TableNames.StockTakeItem);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();
    }
}
