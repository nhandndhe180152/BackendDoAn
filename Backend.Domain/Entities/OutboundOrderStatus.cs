using System;
using System.Collections.Generic;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class OutboundOrderStatus : EntityAuditBase<int>
{
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;

    public virtual ICollection<OutboundOrder> OutboundOrders { get; set; } = new List<OutboundOrder>();
}
