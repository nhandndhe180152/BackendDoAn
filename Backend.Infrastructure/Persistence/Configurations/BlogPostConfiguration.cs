using System;
using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class BlogPostConfiguration : IEntityTypeConfiguration<BlogPost>
{
    public void Configure(EntityTypeBuilder<BlogPost> builder)
    {
        builder.ToTable(TableNames.BlogPost);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd()
             .UseMySqlIdentityColumn();
        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(500);
        builder.Property(x => x.SeoAlias)
            .IsRequired()
           .HasMaxLength(500);
        builder.Property(x => x.Description)
            .IsRequired()
            .HasMaxLength(500);
        builder.Property(x => x.IsApproved).IsRequired().HasDefaultValueSql("(0)");
        builder.Property(x => x.IsShowOnHomePage).IsRequired().HasDefaultValueSql("(0)");
        builder.Property(x => x.IsPopular).IsRequired().HasDefaultValueSql("(0)");
        builder.Property(x => x.CreatedDate).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValueSql("(0)");
        builder.HasMany(x => x.BlogComments)
            .WithOne(x => x.BlogPost)
            .HasForeignKey(x => x.BlogPostId)
            .HasConstraintName("FK_BlogPost_BlogComment")
            .OnDelete(DeleteBehavior.ClientSetNull);
        builder.HasMany(x => x.BlogTags)
            .WithOne(x => x.BlogPost)
            .HasForeignKey(x => x.BlogPostId)
            .HasConstraintName("FK_BlogPost_BlogTag")
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
