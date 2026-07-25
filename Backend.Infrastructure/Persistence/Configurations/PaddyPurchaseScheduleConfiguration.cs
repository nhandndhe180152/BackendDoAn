using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class PaddyPurchaseScheduleConfiguration : IEntityTypeConfiguration<PaddyPurchaseSchedule>
{
    public void Configure(EntityTypeBuilder<PaddyPurchaseSchedule> builder)
    {
        builder.ToTable(TableNames.PaddyPurchaseSchedule);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.ScheduleCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Location).HasMaxLength(500);
        builder.Property(x => x.EstimatedQtyKg).HasColumnType("decimal(18,3)");
        builder.Property(x => x.ExpectedPrice).HasColumnType("decimal(18,2)");
        builder.Property(x => x.Note).HasMaxLength(1000);

        builder.HasIndex(x => new { x.OrganizationId, x.ScheduleCode })
            .IsUnique()
            .HasDatabaseName("UX_PaddyPurchaseSchedule_OrgId_Code");

        builder.HasOne(x => x.Organization)
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Farmer)
            .WithMany(x => x.PaddyPurchaseSchedules)
            .HasForeignKey(x => x.FarmerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Status)
            .WithMany()
            .HasForeignKey(x => x.StatusId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RiceVariety)
            .WithMany()
            .HasForeignKey(x => x.RiceVarietyId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.AssignedUser)
            .WithMany()
            .HasForeignKey(x => x.AssignedUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Warehouse)
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.FarmerId).HasDatabaseName("IX_PaddyPurchaseSchedule_FarmerId");
        builder.HasIndex(x => x.ScheduleDate).HasDatabaseName("IX_PaddyPurchaseSchedule_ScheduleDate");
        builder.HasIndex(x => new { x.WarehouseId, x.StatusId, x.ScheduleDate, x.IsDeleted }).HasDatabaseName("IX_PaddyPurchaseSchedule_Lookup");
    }
}
