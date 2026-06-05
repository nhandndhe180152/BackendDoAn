using System;
using System.Text.Json.Serialization;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class PaymentMethod : EntityCommonBase<int>
{
    [JsonIgnore]
    public virtual ICollection<PaymentTransaction> PaymentTransactions { get; set; }
}
