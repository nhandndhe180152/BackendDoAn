using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class LotStatusConfiguration : IEntityTypeConfiguration<LotStatus>
{
    public void Configure(EntityTypeBuilder<LotStatus> builder)
    {
        builder.ToTable(TableNames.LotStatus);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Color).HasMaxLength(50).IsRequired();
        builder.Property(x => x.IsSellable).HasDefaultValue(true).IsRequired();
    }
}
