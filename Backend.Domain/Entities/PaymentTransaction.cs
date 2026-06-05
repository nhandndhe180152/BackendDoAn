using System;
using System.Text.Json.Serialization;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class PaymentTransaction : EntityAuditBase<int>
{
    public int PaymentMethodId { get; set; }
    public int PaymentStatusId { get; set; }
    public string? PaymentCode { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? BankTransactionId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaidAt { get; set; }
    public string? Note { get; set; }
    public string? Metadata { get; set; }
    [JsonIgnore]
    public virtual PaymentMethod PaymentMethod { get; set; }
    [JsonIgnore]
    public virtual PaymentStatus PaymentStatus { get; set; }
}
