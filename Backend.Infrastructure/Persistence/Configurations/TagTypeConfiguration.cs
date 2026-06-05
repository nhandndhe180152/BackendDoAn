using System;
using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class TagTypeConfiguration : IEntityTypeConfiguration<TagType>
{
    public void Configure(EntityTypeBuilder<TagType> builder)
    {
        builder.ToTable(TableNames.TagType);
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
        builder.HasMany(x => x.Tags)
            .WithOne(x => x.TagType)
            .HasForeignKey(x => x.TagTypeId)
            .HasConstraintName("FK_TagType_Tag")
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
