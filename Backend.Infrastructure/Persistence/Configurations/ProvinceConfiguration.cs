using System;
using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class ProvinceConfiguration : IEntityTypeConfiguration<Province>
{
    public void Configure(EntityTypeBuilder<Province> builder)
    {
        builder.ToTable(TableNames.Province);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd()
             .UseMySqlIdentityColumn();
        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(255);
        builder.Property(e => e.Code)
            .IsRequired()
            .HasMaxLength(255);
        builder.Property(e => e.Slug)
            .IsRequired()
            .HasMaxLength(255);
        builder.Property(e => e.Type)
            .IsRequired()
            .HasMaxLength(255);
        builder.Property(x => x.CreatedDate).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValueSql("(0)");
        builder.HasMany(x => x.Wards)
            .WithOne(x => x.Province)
            .HasForeignKey(x => x.ProvinceId)
            .HasConstraintName("FK_Province_Ward")
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
