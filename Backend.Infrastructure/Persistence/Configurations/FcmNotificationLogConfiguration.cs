using System;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.Configurations;

public class FcmNotificationLogConfiguration : IEntityTypeConfiguration<FcmNotificationLog>
{
    public void Configure(EntityTypeBuilder<FcmNotificationLog> builder)
    {
        builder.ToTable(TableNames.FcmNotificationLog);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Status)
            .HasMaxLength(50)
            .HasDefaultValue("PENDING")
            .IsRequired();

        builder.Property(x => x.AttemptCount)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.LastErrorCode)
            .HasMaxLength(100);

        builder.Property(x => x.ProviderMessageId)
            .HasMaxLength(255);

        builder.Property(x => x.ProcessingBy)
            .HasMaxLength(255);

        builder.Property(x => x.DeduplicationKey)
            .HasMaxLength(255);

        // Indexes for performance
        builder.HasIndex(x => new { x.Status, x.IsDeleted, x.NextRetryAt, x.Id })
            .HasDatabaseName("IX_FcmNotificationLog_EligibleQuery");

        builder.HasIndex(x => x.DeduplicationKey)
            .HasDatabaseName("IX_FcmNotificationLog_DeduplicationKey");

    }
}
