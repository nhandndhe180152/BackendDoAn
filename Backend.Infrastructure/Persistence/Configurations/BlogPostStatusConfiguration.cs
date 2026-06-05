using System;
using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class BlogPostStatusConfiguration : IEntityTypeConfiguration<BlogPostStatus>
{
    public void Configure(EntityTypeBuilder<BlogPostStatus> builder)
    {
        builder.ToTable(TableNames.BlogPostStatus);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd()
             .UseMySqlIdentityColumn();
        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(255);
        builder.Property(x => x.Description)
           .HasMaxLength(500);
        builder.Property(x => x.Color)
            .IsRequired()
            .HasMaxLength(50);
        builder.Property(x => x.CreatedDate).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValueSql("(0)");
        builder.HasMany(x => x.BlogPosts)
            .WithOne(x => x.BlogPostStatus)
            .HasForeignKey(x => x.BlogPostStatusId)
            .HasConstraintName("FK_BlogPostStaus_BlogPost")
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
