using System;
using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable(TableNames.UserSession);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd()
             .UseMySqlIdentityColumn();
        builder.Property(x => x.RefreshToken)
            .IsRequired()
            .HasMaxLength(255);
        builder.Property(x => x.AccessTokenJti)
           .HasMaxLength(500);
        builder.Property(x => x.IsUsed).IsRequired().HasDefaultValueSql("(0)");
        builder.Property(x => x.IsRevoked).IsRequired().HasDefaultValueSql("(0)");
        builder.Property(x => x.CreatedDate).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValueSql("(0)");
    }
}
