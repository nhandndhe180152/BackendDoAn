using System;
using System.Collections.Generic;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class PurchaseOrderStatus : EntityAuditBase<int>
{
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
    
    public virtual ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
}
