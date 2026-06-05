using System;
using System.Collections.Generic;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class OutboundOrder : EntityAuditBase<int>
{
    public int WarehouseId { get; set; }
    public int OutboundOrderStatusId { get; set; }
    public string SOCode { get; set; } = null!;
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerAddress { get; set; }
    public string? Note { get; set; }
    public decimal TotalDispatchedValue { get; set; }
    public DateTime? CompletedDate { get; set; }
    public int? AssignedUserId { get; set; }

    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual OutboundOrderStatus OutboundOrderStatus { get; set; } = null!;
    public virtual User? AssignedUser { get; set; }
    public virtual ICollection<OutboundOrderItem> OutboundOrderItems { get; set; } = new List<OutboundOrderItem>();
}
