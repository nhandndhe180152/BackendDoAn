using System;
using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class FolderUploadConfiguration : IEntityTypeConfiguration<FolderUpload>
{
    public void Configure(EntityTypeBuilder<FolderUpload> builder)
    {
        builder.ToTable(TableNames.FolderUpload);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd()
             .UseMySqlIdentityColumn();
        builder.Property(x => x.FolderName)
            .IsRequired()
            .HasMaxLength(255);
        builder.Property(x => x.FolderPath)
            .IsRequired()
            .HasMaxLength(500);
        builder.Property(x => x.TreeIds)
            .IsRequired()
            .HasMaxLength(255);
        builder.Property(x => x.CreatedDate).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValueSql("(0)");
        builder.HasMany(x => x.FileUploads)
            .WithOne(x => x.FolderUpload)
            .HasForeignKey(x => x.FolderUploadId)
            .HasConstraintName("FK_FolderUpload_FileUpload")
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
