using System;
using System.Collections.Generic;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class OutboundOrder : EntityAuditBase<int>
{
    public int WarehouseId { get; set; }
    public int OutboundOrderStatusId { get; set; }

    /// <summary>Nguồn gốc phiếu xuất — FK → SalesOrder (NOT NULL: mỗi phiếu xuất phải thuộc đơn bán)</summary>
    public int SalesOrderId { get; set; }

    /// <summary>Multi-tenant (nullable ở MVP — chưa bật Global Query Filter)</summary>
    public int? OrganizationId { get; set; }

    public string? Note { get; set; }
    public decimal TotalDispatchedValue { get; set; }
    public DateTime? CompletedDate { get; set; }
    public int? AssignedUserId { get; set; }

    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual OutboundOrderStatus OutboundOrderStatus { get; set; } = null!;
    public virtual SalesOrder SalesOrder { get; set; } = null!;
    public virtual Organization? Organization { get; set; }
    public virtual User? AssignedUser { get; set; }
    public virtual ICollection<OutboundOrderItem> OutboundOrderItems { get; set; } = new List<OutboundOrderItem>();
}

