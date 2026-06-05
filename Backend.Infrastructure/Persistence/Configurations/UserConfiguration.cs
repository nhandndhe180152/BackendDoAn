using System;
using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable(TableNames.User);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd()
             .UseMySqlIdentityColumn();
        builder.Property(x => x.Username)
            .IsRequired()
            .HasMaxLength(255);
        builder.Property(x => x.PasswordHash)
            .IsRequired();
        builder.Property(x => x.FirstName)
            .IsRequired()
            .HasMaxLength(255);
        builder.Property(x => x.LastName)
            .IsRequired()
            .HasMaxLength(255);
        builder.Property(x => x.Email)
            .IsRequired()
            .HasMaxLength(500);
        builder.Property(x => x.PhoneNumber)
            .HasMaxLength(50);
        builder.Property(x => x.PlaceOfIssue)
            .HasMaxLength(500);
        builder.Property(x => x.IdentityNumber)
            .HasMaxLength(255);
        builder.Property(x => x.AddresDetail)
            .HasMaxLength(500);
        builder.Property(x => x.MicrosoftId)
            .HasMaxLength(500);
        builder.Property(x => x.AccessFailedCount).IsRequired().HasDefaultValueSql("(0)");
        builder.Property(x => x.LockEnabled).IsRequired().HasDefaultValueSql("(0)");
        builder.Property(x => x.CreatedDate).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValueSql("(0)");
        builder.HasMany(x => x.UserRoles)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.UserId)
            .HasConstraintName("FK_User_UserRole")
            .OnDelete(DeleteBehavior.ClientSetNull);
        builder.HasMany(x => x.BlogPosts)
            .WithOne(x => x.Author)
            .HasForeignKey(x => x.AuthorId)
            .HasConstraintName("FK_User_BlogPost")
            .OnDelete(DeleteBehavior.ClientSetNull);
        builder.HasMany(x => x.UserNotifications)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.UserId)
            .HasConstraintName("FK_User_UserNotification")
            .OnDelete(DeleteBehavior.ClientSetNull);
        builder.HasMany(x => x.UserVerificationTokens)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.UserId)
            .HasConstraintName("FK_User_UserVerificationToken")
            .OnDelete(DeleteBehavior.ClientSetNull);
        builder.HasMany(x => x.UserDevices)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.UserId)
            .HasConstraintName("FK_User_UserDevice")
            .OnDelete(DeleteBehavior.ClientSetNull);
        builder.HasMany(x => x.UserSessions)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.UserId)
            .HasConstraintName("FK_User_UserSession")
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
