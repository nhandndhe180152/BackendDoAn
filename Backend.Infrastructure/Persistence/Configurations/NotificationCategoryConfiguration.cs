using System;
using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class NotificationCategoryConfiguration : IEntityTypeConfiguration<NotificationCategory>
{
    public void Configure(EntityTypeBuilder<NotificationCategory> builder)
    {
        builder.ToTable(TableNames.NotificationCategory);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd()
             .UseMySqlIdentityColumn();
        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(255);
        builder.Property(x => x.Description)
           .HasMaxLength(500);
        builder.Property(x => x.Color)
            .IsRequired()
            .HasMaxLength(50);
        builder.Property(x => x.CreatedDate).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValueSql("(0)");
        builder.HasMany(x => x.Notifications)
            .WithOne(x => x.NotificationCategory)
            .HasForeignKey(x => x.NotificationCategoryId)
            .HasConstraintName("FK_NotificationCategory_Notification")
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
