using System;

namespace Backend.Application.DTOs.PartyDebts;

public class DebtTransactionDetailDto
{
    public int Id { get; set; }
    public int PartyDebtId { get; set; }
    public string TransactionType { get; set; } = null!;
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public string? RefType { get; set; }
    public int? RefId { get; set; }
    public DateTime TransactionDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedDate { get; set; }
}
