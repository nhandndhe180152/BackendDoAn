using System;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class ActionConfiguration : IEntityTypeConfiguration<Domain.Entities.Action>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.Action> builder)
    {
        builder.ToTable(TableNames.Action);
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
        builder.HasMany(x => x.Permissions)
            .WithOne(x => x.Action)
            .HasForeignKey(x => x.ActionId)
            .HasConstraintName("FK_Action_Permission")
            .OnDelete(DeleteBehavior.ClientSetNull);
        builder.HasMany(x => x.ActionInMenus)
            .WithOne(x => x.Action)
            .HasForeignKey(x => x.ActionId)
            .HasConstraintName("FK_Action_ActionInMenu")
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
