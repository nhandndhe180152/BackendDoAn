using System;

namespace Backend.Domain.Aggregates;

public class PaymentTransactionAggregate
{
    public int Id { get; set; }
    public int OfficeId { get; set; }
    public string OfficeName { get; set; }
    public int UserId { get; set; }
    public string UserFullName { get; set; }
    public int NotarizationRequestId { get; set; }
    public string NotarizationRequestName { get; set; }
    public int PaymentMethodId { get; set; }
    public string PaymentMethodName { get; set; }
    public int PaymentStatusId { get; set; }
    public string PaymentStatusName { get; set; }
    public string PaymentStatusColor { get; set; }
    public string? PaymentCode { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? BankTransactionId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaidAtDate { get; set; }
    public string? Note { get; set; }
    public string? Metadata { get; set; }
    public DateTime CreatedDate { get; set; }

}
