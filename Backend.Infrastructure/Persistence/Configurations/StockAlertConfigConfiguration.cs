using System;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.Configurations;

public class StockAlertConfigConfiguration : IEntityTypeConfiguration<StockAlertConfig>
{
    public void Configure(EntityTypeBuilder<StockAlertConfig> builder)
    {
        builder.ToTable(TableNames.StockAlertConfig);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();
    }
}
