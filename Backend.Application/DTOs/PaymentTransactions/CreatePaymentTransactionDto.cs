using System;

namespace Backend.Application.DTOs.PaymentTransactions;

public class CreatePaymentTransactionDto
{
    public int NotarizationRequestId { get; set; }
    public int PaymentMethodId { get; set; }
    public int PaymentStatusId { get; set; }
    public string? PaymentCode { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? BankTransactionId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaidAt { get; set; }
    public string? Note { get; set; }
    public string? Metadata { get; set; }
    public int? CreatedBy { get; set; }
}
