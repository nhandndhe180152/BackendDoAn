using System;
using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class NotificationTypeConfiguration : IEntityTypeConfiguration<NotificationType>
{
    public void Configure(EntityTypeBuilder<NotificationType> builder)
    {
        builder.ToTable(TableNames.NotificationType);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd()
             .UseMySqlIdentityColumn();
        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(255);
        builder.Property(e => e.Description)
              .HasMaxLength(500);
        builder.Property(x => x.CreatedDate).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValueSql("(0)");

        builder.HasMany(x => x.Notifications)
            .WithOne(x => x.NotificationType)
            .HasForeignKey(x => x.NotificationTypeId)
            .HasConstraintName("FK_NotificationType_Notification")
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
