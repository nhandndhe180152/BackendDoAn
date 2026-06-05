using System;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.Configurations;

public class IotDeviceConfiguration : IEntityTypeConfiguration<IotDevice>
{
    public void Configure(EntityTypeBuilder<IotDevice> builder)
    {
        builder.ToTable(TableNames.IotDevice);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd()
            .UseMySqlIdentityColumn();

        builder.Property(x => x.WarehouseId)
            .IsRequired();

        builder.Property(x => x.ApiKeyHash)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.DeviceName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.DeviceCode)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(x => x.DeviceCode)
            .IsUnique()
            .HasDatabaseName("UQ_IotDevice_DeviceCode");

        builder.Property(x => x.DeviceType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Location)
            .HasMaxLength(255);

        builder.Property(x => x.MqttTopic)
            .HasMaxLength(500);

        builder.Property(x => x.IsOnline)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.CreatedDate)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.HasOne(x => x.Warehouse)
            .WithMany(x => x.IotDevices)
            .HasForeignKey(x => x.WarehouseId)
            .HasConstraintName("FK_IotDevice_Warehouse")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.IotWeightLogs)
            .WithOne(x => x.IotDevice)
            .HasForeignKey(x => x.IoTDeviceId)
            .HasConstraintName("FK_IotWeightLog_IotDevice")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.IotDeviceCommands)
            .WithOne(x => x.IotDevice)
            .HasForeignKey(x => x.IoTDeviceId)
            .HasConstraintName("FK_IotDeviceCommand_IotDevice")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
