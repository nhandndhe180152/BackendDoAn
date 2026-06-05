using System;
using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class BlogLayoutConfiguration : IEntityTypeConfiguration<BlogPostLayout>
{
    public void Configure(EntityTypeBuilder<BlogPostLayout> builder)
    {
        builder.ToTable(TableNames.BlogPostLayout);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd()
             .UseMySqlIdentityColumn();
        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(255);
        builder.Property(x => x.Description)
           .HasMaxLength(500);
        builder.Property(x => x.CreatedDate).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValueSql("(0)");
        builder.HasMany(x => x.BlogPosts)
            .WithOne(x => x.BlogPostLayout)
            .HasForeignKey(x => x.BlogPostLayoutId)
            .HasConstraintName("FK_BlogPostLayout_BlogPost")
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
