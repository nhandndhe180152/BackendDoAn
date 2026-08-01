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

    /// <summary>
    /// Khóa chống gửi thanh toán lặp từ client. Cùng một RequestId chỉ được ghi một lần.
    /// </summary>
    public string? RequestId { get; set; }
}
