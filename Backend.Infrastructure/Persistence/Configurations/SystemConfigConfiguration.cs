using System;
using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class SystemConfigConfiguration : IEntityTypeConfiguration<SystemConfig>
{
    public void Configure(EntityTypeBuilder<SystemConfig> builder)
    {
        builder.ToTable(TableNames.SystemConfig);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd()
             .UseMySqlIdentityColumn();
        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(255);
        builder.Property(x => x.Description)
           .HasMaxLength(500);
        builder.Property(x => x.ConfigKey)
            .IsRequired()
            .HasMaxLength(255);
        builder.Property(x => x.ConfigValue)
            .IsRequired();
        builder.Property(x => x.CreatedDate).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValueSql("(0)");
    }
}
