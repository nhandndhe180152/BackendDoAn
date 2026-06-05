using System;
using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class UserDeviceConfiguration : IEntityTypeConfiguration<UserDevice>
{
    public void Configure(EntityTypeBuilder<UserDevice> builder)
    {
        builder.ToTable(TableNames.UserDevice);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd()
             .UseMySqlIdentityColumn();
        builder.Property(x => x.DeviceName)
            .HasMaxLength(255);
        builder.Property(x => x.Platform)
           .HasMaxLength(255);
        builder.Property(x => x.OsVersion)
            .HasMaxLength(255);
        builder.Property(x => x.AppVersion)
           .HasMaxLength(255);
        builder.Property(x => x.CreatedDate).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValueSql("(0)");
        builder.HasMany(x => x.UserSessions)
            .WithOne(x => x.UserDevice)
            .HasForeignKey(x => x.UserDeviceId)
            .HasConstraintName("FK_UserDevice_UserSession")
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
