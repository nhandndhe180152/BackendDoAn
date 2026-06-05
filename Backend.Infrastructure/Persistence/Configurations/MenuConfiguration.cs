using System;
using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class MenuConfiguration : IEntityTypeConfiguration<Menu>
{
    public void Configure(EntityTypeBuilder<Menu> builder)
    {
        builder.ToTable(TableNames.Menu);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd()
             .UseMySqlIdentityColumn();
        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(255);
        builder.Property(x => x.TreeIds)
            .IsRequired()
            .HasMaxLength(255);
        builder.Property(x => x.MenuType)
            .IsRequired()
            .HasMaxLength(255);
        builder.Property(x => x.IsAdminOnly)
            .HasDefaultValueSql("(0)");
        builder.Property(x => x.CreatedDate).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValueSql("(0)");
        builder.HasMany(x => x.Permissions)
            .WithOne(x => x.Menu)
            .HasForeignKey(x => x.MenuId)
            .HasConstraintName("FK_Menu_Permission")
            .OnDelete(DeleteBehavior.ClientSetNull);
        builder.HasMany(x => x.ActionInMenus)
            .WithOne(x => x.Menu)
            .HasForeignKey(x => x.MenuId)
            .HasConstraintName("FK_Menu_ActionInMenu")
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
