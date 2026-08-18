using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class SalesOrderConfiguration : IEntityTypeConfiguration<SalesOrder>
{
    public void Configure(EntityTypeBuilder<SalesOrder> builder)
    {
        builder.ToTable(TableNames.SalesOrder);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.SOCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Channel).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ShippingAddress).HasMaxLength(500);
        builder.Property(x => x.TotalAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.DepositAmount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.CancelReason).HasMaxLength(500);
        builder.Property(x => x.RequiresMilling).HasDefaultValue(false);

        builder.HasIndex(x => new { x.OrganizationId, x.SOCode })
            .IsUnique()
            .HasDatabaseName("UX_SalesOrder_OrgId_SOCode");

        builder.HasOne(x => x.Organization)
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Customer)
            .WithMany(x => x.SalesOrders)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Status)
            .WithMany()
            .HasForeignKey(x => x.StatusId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Warehouse)
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.CustomerId).HasDatabaseName("IX_SalesOrder_CustomerId");
        builder.HasIndex(x => x.OrderDate).HasDatabaseName("IX_SalesOrder_OrderDate");
        builder.HasIndex(x => x.StatusId).HasDatabaseName("IX_SalesOrder_StatusId");
    }
}
