using System;
using System.Collections.Generic;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class InboundOrderStatus : EntityAuditBase<int>
{
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
    
    public virtual ICollection<InboundOrder> InboundOrders { get; set; } = new List<InboundOrder>();
}
