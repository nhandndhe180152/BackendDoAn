using System;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.Configurations;

public class IotDeviceCommandConfiguration : IEntityTypeConfiguration<IotDeviceCommand>
{
    public void Configure(EntityTypeBuilder<IotDeviceCommand> builder)
    {
        builder.ToTable(TableNames.IotDeviceCommand);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd()
            .UseMySqlIdentityColumn();

        builder.Property(x => x.IoTDeviceId)
            .IsRequired();

        builder.Property(x => x.CommandCode)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(x => x.CommandCode)
            .IsUnique()
            .HasDatabaseName("UQ_IotDeviceCommand_CommandCode");

        builder.Property(x => x.CommandType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Payload)
            .HasColumnType("json");

        builder.Property(x => x.Status)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.RequestedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.Property(x => x.ResultMessage)
            .HasMaxLength(500);

        builder.Property(x => x.RetryCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.CreatedDate)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.HasIndex(x => new { x.IoTDeviceId, x.Status })
            .HasDatabaseName("IX_IotDeviceCommand_Device_Status");

        builder.HasIndex(x => x.RequestedByUserId)
            .HasDatabaseName("IX_IotDeviceCommand_RequestedByUserId");

        builder.HasOne(x => x.IotDevice)
            .WithMany(x => x.IotDeviceCommands)
            .HasForeignKey(x => x.IoTDeviceId)
            .HasConstraintName("FK_IotDeviceCommand_IotDevice")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RequestedByUser)
            .WithMany()
            .HasForeignKey(x => x.RequestedByUserId)
            .HasConstraintName("FK_IotDeviceCommand_RequestedByUser")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
