using System;
using Backend.Share.Entities;

namespace Backend.Application.DTOs.PaymentTransactions;

public class PaymentTransactionDetailDto
{
    public int Id { get; set; }
    public int NotarizationRequestId { get; set; }
    //public string NotarizationRequestName { get; set; }
    public int PaymentMethodId { get; set; }
    //public string PaymentMethodName { get; set; }
    public int PaymentStatusId { get; set; }
    //public string PaymentStatusName { get; set; }
    public string? PaymentCode { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? BankTransactionId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaidAt { get; set; }
    public string? Note { get; set; }
    public string? Metadata { get; set; }
    public DateTime CreatedDate { get; set; }
    public DataItem<int> PaymentMethod { get; set; } = new DataItem<int>();
    public DataItem<int> PaymentStatus { get; set; } = new DataItem<int>();

}
