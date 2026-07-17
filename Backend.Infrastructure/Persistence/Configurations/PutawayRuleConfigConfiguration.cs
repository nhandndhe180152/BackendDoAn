using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class PutawayRuleConfigConfiguration : IEntityTypeConfiguration<PutawayRuleConfig>
{
    public void Configure(EntityTypeBuilder<PutawayRuleConfig> builder)
    {
        builder.ToTable(TableNames.PutawayRuleConfig);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.CapacityWeight).HasColumnType("decimal(5,4)").HasDefaultValue(0.4000m);
        builder.Property(x => x.OccupancyWeight).HasColumnType("decimal(5,4)").HasDefaultValue(0.3000m);
        builder.Property(x => x.CategoryWeight).HasColumnType("decimal(5,4)").HasDefaultValue(0.2000m);
        builder.Property(x => x.PriorityWeight).HasColumnType("decimal(5,4)").HasDefaultValue(0.1000m);
        builder.Property(x => x.SameProductScore).HasColumnType("decimal(5,4)").HasDefaultValue(1.0000m);
        builder.Property(x => x.EmptyColumnScore).HasColumnType("decimal(5,4)").HasDefaultValue(0.6000m);

        builder.HasOne(x => x.Warehouse)
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
