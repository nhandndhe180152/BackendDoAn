using System;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.Configurations;

public class IotWeightLogConfiguration : IEntityTypeConfiguration<IotWeightLog>
{
    public void Configure(EntityTypeBuilder<IotWeightLog> builder)
    {
        builder.ToTable(TableNames.IotWeightLog);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd()
            .UseMySqlIdentityColumn();

        builder.Property(x => x.IoTDeviceId)
            .IsRequired();

        builder.Property(x => x.ProductVariantId)
            .IsRequired(false);

        builder.Property(x => x.ReferenceType)
            .HasMaxLength(50);

        builder.Property(x => x.WeightKg)
            .HasColumnType("decimal(10,3)")
            .IsRequired();

        builder.Property(x => x.RawValue)
            .HasColumnType("decimal(18,6)");

        builder.Property(x => x.Unit)
            .IsRequired()
            .HasMaxLength(10)
            .HasDefaultValue("kg");

        builder.Property(x => x.IsStable)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.MeasuredAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.Property(x => x.ReceivedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.Property(x => x.IsConfirmed)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.RequestIpAddress)
            .HasMaxLength(100);

        builder.Property(x => x.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.CreatedDate)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.HasIndex(x => new { x.IoTDeviceId, x.MeasuredAt })
            .HasDatabaseName("IX_IotWeightLog_Device_MeasuredAt");

        builder.HasIndex(x => new { x.ReferenceType, x.ReferenceId, x.ReferenceItemId })
            .HasDatabaseName("IX_IotWeightLog_Reference");

        builder.HasIndex(x => x.ProductVariantId)
            .HasDatabaseName("IX_IotWeightLog_ProductVariant");

        builder.HasOne(x => x.IotDevice)
            .WithMany(x => x.IotWeightLogs)
            .HasForeignKey(x => x.IoTDeviceId)
            .HasConstraintName("FK_IotWeightLog_IotDevice")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProductVariant)
            .WithMany()
            .HasForeignKey(x => x.ProductVariantId)
            .HasConstraintName("FK_IotWeightLog_ProductVariant")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
