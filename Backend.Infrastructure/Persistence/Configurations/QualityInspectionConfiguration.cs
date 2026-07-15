using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class QualityInspectionConfiguration : IEntityTypeConfiguration<QualityInspection>
{
    public void Configure(EntityTypeBuilder<QualityInspection> builder)
    {
        builder.ToTable(TableNames.QualityInspection);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.MoisturePercent).HasColumnType("decimal(5,2)");
        builder.Property(x => x.ImpurityPercent).HasColumnType("decimal(5,2)");
        builder.Property(x => x.MoldLevel).HasMaxLength(50);
        builder.Property(x => x.PestLevel).HasMaxLength(50);
        builder.Property(x => x.PackagingStatus).HasMaxLength(50);
        builder.Property(x => x.Handling).HasMaxLength(200);
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.PassedInspection).HasDefaultValue(false);

        builder.HasOne(x => x.PaddyLot)
            .WithMany(x => x.QualityInspections)
            .HasForeignKey(x => x.PaddyLotId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Inspector)
            .WithMany()
            .HasForeignKey(x => x.InspectorId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.PaddyLotId).HasDatabaseName("IX_QualityInspection_PaddyLotId");
        builder.HasIndex(x => x.InspectedAt).HasDatabaseName("IX_QualityInspection_InspectedAt");
    }
}
