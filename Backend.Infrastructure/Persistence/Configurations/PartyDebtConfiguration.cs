using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class PartyDebtConfiguration : IEntityTypeConfiguration<PartyDebt>
{
    public void Configure(EntityTypeBuilder<PartyDebt> builder)
    {
        builder.ToTable(TableNames.PartyDebt);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.PartyType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Direction).HasMaxLength(20).IsRequired();
        builder.Property(x => x.OpeningBalance).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.CurrentBalance).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.CreditLimit).HasColumnType("decimal(18,2)");
        builder.Property(x => x.IsActive).HasDefaultValue(true);

        builder.HasOne(x => x.Organization)
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.OrganizationId, x.PartyType, x.PartyId })
            .HasDatabaseName("IX_PartyDebt_OrgId_PartyType_PartyId");
    }
}
