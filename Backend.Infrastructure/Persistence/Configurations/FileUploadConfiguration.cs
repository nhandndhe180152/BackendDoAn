using System;
using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class FileUploadConfiguration : IEntityTypeConfiguration<FileUpload>
{
    public void Configure(EntityTypeBuilder<FileUpload> builder)
    {
        builder.ToTable(TableNames.FileUpload);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd()
             .UseMySqlIdentityColumn();
        builder.Property(x => x.FileName)
            .IsRequired()
            .HasMaxLength(500);
        builder.Property(x => x.FileType)
            .IsRequired()
            .HasMaxLength(500);
        builder.Property(x => x.FileKey)
            .IsRequired()
            .HasMaxLength(1000);
        builder.Property(x => x.CreatedDate).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValueSql("(0)");
        builder.HasMany(x => x.Users)
            .WithOne(x => x.Avatar)
            .HasForeignKey(x => x.AvatarId)
            .HasConstraintName("FK_FileUpload_User")
            .OnDelete(DeleteBehavior.ClientSetNull);
        builder.HasMany(x => x.BlogPosts)
            .WithOne(x => x.CoverImage)
            .HasForeignKey(x => x.CoverImageId)
            .HasConstraintName("FK_FileUpload_BlogPost")
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
