using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class MillingYieldConfigConfiguration : IEntityTypeConfiguration<MillingYieldConfig>
{
    public void Configure(EntityTypeBuilder<MillingYieldConfig> builder)
    {
        builder.ToTable(TableNames.MillingYieldConfig);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.MoistureFrom).HasColumnType("decimal(5,2)");
        builder.Property(x => x.MoistureTo).HasColumnType("decimal(5,2)");
        builder.Property(x => x.YieldRate).HasColumnType("decimal(6,4)").IsRequired();
        builder.Property(x => x.BrokenRiceRate).HasColumnType("decimal(6,4)");
        builder.Property(x => x.BranRate).HasColumnType("decimal(6,4)");
        builder.Property(x => x.HuskRate).HasColumnType("decimal(6,4)");
        builder.Property(x => x.IsActive).HasDefaultValue(true);

        builder.HasOne(x => x.Organization)
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RiceVariety)
            .WithMany(x => x.MillingYieldConfigs)
            .HasForeignKey(x => x.RiceVarietyId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.OrganizationId, x.RiceVarietyId, x.IsActive })
            .HasDatabaseName("IX_MillingYieldConfig_OrgId_VarietyId_Active");
    }
}
