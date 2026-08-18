using Backend.Domain.Entities;
using Backend.Infrastructure.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class DebtTransactionConfiguration : IEntityTypeConfiguration<DebtTransaction>
{
    public void Configure(EntityTypeBuilder<DebtTransaction> builder)
    {
        builder.ToTable(TableNames.DebtTransaction);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.TransactionType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.BalanceAfter).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.RefType).HasMaxLength(50);
        builder.Property(x => x.Note).HasMaxLength(500);

        builder.HasOne(x => x.PartyDebt)
            .WithMany(x => x.DebtTransactions)
            .HasForeignKey(x => x.PartyDebtId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.PartyDebtId).HasDatabaseName("IX_DebtTransaction_PartyDebtId");
        builder.HasIndex(x => x.TransactionDate).HasDatabaseName("IX_DebtTransaction_TransactionDate");
        builder.HasIndex(x => new { x.RefType, x.RefId }).HasDatabaseName("IX_DebtTransaction_RefType_RefId");

        builder.Property(x => x.DeduplicationKey).HasMaxLength(200);
        builder.HasIndex(x => x.DeduplicationKey)
            .IsUnique()
            .HasDatabaseName("UX_DebtTransaction_DeduplicationKey");
    }
}
