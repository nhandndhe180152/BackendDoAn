using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class CustomerFeedbackConfiguration : IEntityTypeConfiguration<CustomerFeedback>
{
    public void Configure(EntityTypeBuilder<CustomerFeedback> builder)
    {
        builder.ToTable(TableNames.CustomerFeedback);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.FeedbackType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Severity).HasMaxLength(50);
        builder.Property(x => x.ResolutionStatus).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ResolutionNote).HasMaxLength(2000);

        builder.HasOne(x => x.SalesOrder)
            .WithMany()
            .HasForeignKey(x => x.SalesOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.OutboundOrder)
            .WithMany()
            .HasForeignKey(x => x.OutboundOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.OutboundOrderItem)
            .WithMany()
            .HasForeignKey(x => x.OutboundOrderItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProductVariant)
            .WithMany()
            .HasForeignKey(x => x.ProductVariantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PaddyLotBagAllocation)
            .WithMany()
            .HasForeignKey(x => x.PaddyLotBagAllocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ResolvedByUser)
            .WithMany()
            .HasForeignKey(x => x.ResolvedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
