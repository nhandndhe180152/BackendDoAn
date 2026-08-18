using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class PaddyLotBagContentConfiguration : IEntityTypeConfiguration<PaddyLotBagContent>
{
    public void Configure(EntityTypeBuilder<PaddyLotBagContent> builder)
    {
        builder.ToTable(TableNames.PaddyLotBagContent);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.WeightKg).HasColumnType("decimal(18,3)").IsRequired();
        builder.HasIndex(x => new { x.BagId, x.LotId });
        builder.HasIndex(x => x.LotId);
        builder.HasOne(x => x.Bag).WithMany(x => x.Contents).HasForeignKey(x => x.BagId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Lot).WithMany(x => x.BagContents).HasForeignKey(x => x.LotId).OnDelete(DeleteBehavior.Restrict);
    }
}
