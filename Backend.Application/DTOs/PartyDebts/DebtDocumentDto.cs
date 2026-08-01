using System;

namespace Backend.Application.DTOs.PartyDebts;

public class DebtDocumentDto
{
    public int PartyDebtId { get; set; }
    public int ChargeTransactionId { get; set; }
    public string PartyType { get; set; } = string.Empty;
    public int PartyId { get; set; }
    public string PartyCode { get; set; } = string.Empty;
    public string PartyName { get; set; } = string.Empty;
    public string? PartyPhone { get; set; }
    public string Direction { get; set; } = string.Empty;
    public string? RefType { get; set; }
    public int? RefId { get; set; }
    public string DocumentCode { get; set; } = string.Empty;
    public string? DocumentUrl { get; set; }
    public DateTime TransactionDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Note { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal OutstandingAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public int DaysOverdue { get; set; }
}

public class DebtTransactionListItemDto : DebtTransactionDetailDto
{
    public string PartyType { get; set; } = string.Empty;
    public int PartyId { get; set; }
    public string PartyCode { get; set; } = string.Empty;
    public string PartyName { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string DocumentCode { get; set; } = string.Empty;
    public string? CreatedByName { get; set; }
}
