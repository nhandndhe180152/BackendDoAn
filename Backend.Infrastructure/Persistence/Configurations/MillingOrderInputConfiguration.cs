using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class MillingOrderInputConfiguration : IEntityTypeConfiguration<MillingOrderInput>
{
    public void Configure(EntityTypeBuilder<MillingOrderInput> builder)
    {
        builder.ToTable(TableNames.MillingOrderInput);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.ReservedWeightKg).HasColumnType("decimal(18,3)");
        builder.Property(x => x.ConsumedWeightKg).HasColumnType("decimal(18,3)").IsRequired();
        builder.Property(x => x.Note).HasMaxLength(500);

        builder.HasOne(x => x.MillingOrder)
            .WithMany(x => x.MillingOrderInputs)
            .HasForeignKey(x => x.MillingOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.PaddyLot)
            .WithMany(x => x.MillingOrderInputs)
            .HasForeignKey(x => x.PaddyLotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Location)
            .WithMany()
            .HasForeignKey(x => x.LocationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.MillingOrderId).HasDatabaseName("IX_MillingOrderInput_MillingOrderId");
        builder.HasIndex(x => x.PaddyLotId).HasDatabaseName("IX_MillingOrderInput_PaddyLotId");
    }
}
