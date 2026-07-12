using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

/// <summary>
/// Từng lần phát sinh hoặc thanh toán công nợ, tham chiếu chứng từ nguồn.
/// </summary>
public class DebtTransaction : EntityAuditBase<int>
{
    public int PartyDebtId { get; set; }

    /// <summary>CHARGE (phát sinh nợ) | PAYMENT (thanh toán)</summary>
    public string TransactionType { get; set; } = null!;

    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }

    /// <summary>PADDY_RECEIPT | SALES_ORDER | MANUAL…</summary>
    public string? RefType { get; set; }

    public int? RefId { get; set; }
    public DateTime TransactionDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Note { get; set; }

    // Navigation
    public virtual PartyDebt PartyDebt { get; set; } = null!;
}
