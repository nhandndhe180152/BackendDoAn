using System;

namespace Backend.Application.DTOs.PartyDebts;

public class CreateDebtTransactionDto
{
    public int PartyDebtId { get; set; }

    /// <summary>CHARGE | PAYMENT</summary>
    public string TransactionType { get; set; } = null!;

    public decimal Amount { get; set; }
    public string? RefType { get; set; }
    public int? RefId { get; set; }
    public DateTime TransactionDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Note { get; set; }
    public int? CreatedBy { get; set; }
}
