using System;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.Configurations;

public class StockTakeConfiguration : IEntityTypeConfiguration<StockTake>
{
    public void Configure(EntityTypeBuilder<StockTake> builder)
    {
        builder.ToTable(TableNames.StockTake);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.STCode).HasMaxLength(50);

        //builder.Property(x => x.ApprovalNote).HasMaxLength(500);

        builder.HasIndex(x => x.STCode)
            .IsUnique()
            .HasDatabaseName("UX_StockTake_STCode");

        builder.HasIndex(x => x.ApprovedByUserId)
            .HasDatabaseName("IX_StockTake_ApprovedByUserId");
    }
}
