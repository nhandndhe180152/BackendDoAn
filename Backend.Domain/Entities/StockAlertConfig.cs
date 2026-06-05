using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class StockAlertConfig : EntityAuditBase<int>
{
    public int WarehouseId { get; set; }
    public int? ProductVariantId { get; set; }
    public int MinThreshold { get; set; }
    public bool IsActive { get; set; }

    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual ProductVariant? ProductVariant { get; set; }
}
