using System;
using System.Text.Json.Serialization;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class PaymentStatus : EntityCommonBase<int>
{
    public string Color { get; set; } = null!;
    [JsonIgnore]
    public virtual ICollection<PaymentTransaction> PaymentTransactions { get; set; }
}
