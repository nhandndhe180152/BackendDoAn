using System;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.Configurations;

public class StockTakeStatusConfiguration : IEntityTypeConfiguration<StockTakeStatus>
{
    public void Configure(EntityTypeBuilder<StockTakeStatus> builder)
    {
        builder.ToTable(TableNames.StockTakeStatus);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();
    }
}
