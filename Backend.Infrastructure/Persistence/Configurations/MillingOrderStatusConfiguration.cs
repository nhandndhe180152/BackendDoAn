using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class MillingOrderStatusConfiguration : IEntityTypeConfiguration<MillingOrderStatus>
{
    public void Configure(EntityTypeBuilder<MillingOrderStatus> builder)
    {
        builder.ToTable(TableNames.MillingOrderStatus);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Color).HasMaxLength(50).IsRequired();
    }
}
