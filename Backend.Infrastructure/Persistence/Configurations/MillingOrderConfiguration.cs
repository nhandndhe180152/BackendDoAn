using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class MillingOrderConfiguration : IEntityTypeConfiguration<MillingOrder>
{
    public void Configure(EntityTypeBuilder<MillingOrder> builder)
    {
        builder.ToTable(TableNames.MillingOrder);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.MillingCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.YieldRateUsed).HasColumnType("decimal(6,4)").IsRequired();
        builder.Property(x => x.TotalRiceOutputKg).HasColumnType("decimal(18,3)").IsRequired();
        builder.Property(x => x.ComputedPaddyKg).HasColumnType("decimal(18,3)").IsRequired();
        builder.Property(x => x.ActualPaddyInputKg).HasColumnType("decimal(18,3)");
        builder.Property(x => x.ActualYieldRate).HasColumnType("decimal(8,6)");
        builder.Property(x => x.ByproductKg).HasColumnType("decimal(18,3)");
        builder.Property(x => x.LossKg).HasColumnType("decimal(18,3)");
        builder.Property(x => x.MachineRef).HasMaxLength(100);
        builder.Property(x => x.TotalCost).HasColumnType("decimal(18,2)");

        builder.HasIndex(x => new { x.OrganizationId, x.MillingCode })
            .IsUnique()
            .HasDatabaseName("UX_MillingOrder_OrgId_Code");

        builder.HasOne(x => x.Organization)
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Status)
            .WithMany()
            .HasForeignKey(x => x.StatusId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Warehouse)
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RiceVariety)
            .WithMany()
            .HasForeignKey(x => x.RiceVarietyId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.SalesOrder)
            .WithMany(x => x.MillingOrders)
            .HasForeignKey(x => x.SalesOrderId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Operator)
            .WithMany()
            .HasForeignKey(x => x.OperatorId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.StatusId).HasDatabaseName("IX_MillingOrder_StatusId");
        builder.HasIndex(x => x.RiceVarietyId).HasDatabaseName("IX_MillingOrder_RiceVarietyId");
        builder.HasIndex(x => x.StartedAt).HasDatabaseName("IX_MillingOrder_StartedAt");
    }
}
