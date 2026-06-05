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
    }
}
