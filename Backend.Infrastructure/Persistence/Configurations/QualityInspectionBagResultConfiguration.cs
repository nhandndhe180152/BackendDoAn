using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class QualityInspectionBagResultConfiguration : IEntityTypeConfiguration<QualityInspectionBagResult>
{
    public void Configure(EntityTypeBuilder<QualityInspectionBagResult> builder)
    {
        builder.ToTable(TableNames.QualityInspectionBagResult);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.MoisturePercent).HasColumnType("decimal(5,2)");
        builder.Property(x => x.ImpurityPercent).HasColumnType("decimal(5,2)");
        builder.Property(x => x.MoldLevel).HasMaxLength(50);
        builder.Property(x => x.PestLevel).HasMaxLength(50);
        builder.Property(x => x.PackagingStatus).HasMaxLength(50);
        builder.Property(x => x.QualityResult).HasMaxLength(30);
        builder.Property(x => x.Disposition).HasMaxLength(30);
        builder.Property(x => x.Handling).HasMaxLength(200);
        builder.Property(x => x.Note).HasMaxLength(1000);

        // FK: QualityInspection (header)
        builder.HasOne(x => x.Inspection)
            .WithMany(x => x.BagResults)
            .HasForeignKey(x => x.QualityInspectionId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK: PaddyLotBag
        builder.HasOne(x => x.Bag)
            .WithMany(x => x.QualityResults)
            .HasForeignKey(x => x.BagId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK: Inspector (nullable)
        builder.HasOne(x => x.Inspector)
            .WithMany()
            .HasForeignKey(x => x.InspectorId)
            .OnDelete(DeleteBehavior.SetNull);

        // ── Indexes bắt buộc theo thiết kế ────────────────────────────────────
        // UNIQUE: một bag chỉ có 1 kết quả hiện hành trong 1 inspection
        builder.HasIndex(x => new { x.QualityInspectionId, x.BagId })
            .IsUnique()
            .HasDatabaseName("UQ_QualityInspectionBagResult_InspectionId_BagId");

        builder.HasIndex(x => new { x.BagId, x.InspectedAt })
            .HasDatabaseName("IX_QualityInspectionBagResult_BagId_InspectedAt");

        builder.HasIndex(x => new { x.QualityInspectionId, x.Disposition })
            .HasDatabaseName("IX_QualityInspectionBagResult_InspectionId_Disposition");
    }
}
