using System;
using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable(TableNames.Notification);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd()
             .UseMySqlIdentityColumn();
        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(255);
        builder.Property(x => x.Content)
            .IsRequired()
            .HasMaxLength(500);
        builder.Property(x => x.DirectionId)
            .HasMaxLength(255);
        builder.Property(x => x.CreatedDate).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValueSql("(0)");
        builder.HasMany(x => x.UserNotifications)
            .WithOne(x => x.Notification)
            .HasForeignKey(x => x.NotificationId)
            .HasConstraintName("FK_Notification_UserNotification")
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
