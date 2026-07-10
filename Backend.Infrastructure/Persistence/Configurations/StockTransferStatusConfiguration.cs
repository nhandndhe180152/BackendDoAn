using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class StockTransferStatusConfiguration : IEntityTypeConfiguration<StockTransferStatus>
{
    public void Configure(EntityTypeBuilder<StockTransferStatus> builder)
    {
        builder.ToTable(TableNames.StockTransferStatus);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Color).HasMaxLength(50).IsRequired();
    }
}
