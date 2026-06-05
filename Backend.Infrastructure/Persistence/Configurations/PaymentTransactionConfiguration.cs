using System;
using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable(TableNames.PaymentTransaction);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd()
             .UseMySqlIdentityColumn();
        builder.Property(x => x.PaymentCode)
            .HasMaxLength(255);
        builder.Property(x => x.InvoiceNumber)
           .HasMaxLength(255);
        builder.Property(x => x.BankTransactionId)
            .HasMaxLength(500);
        builder.Property(x => x.Note)
            .HasMaxLength(500);
        builder.Property(x => x.Amount)
            .HasColumnType("decimal(18,2)");
        builder.Property(x => x.CreatedDate).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValueSql("(0)");
    }
}
