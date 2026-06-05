using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class InventoryTransaction : EntityAuditBase<int>
{
    public int InventoryId { get; set; }
    public int WarehouseId { get; set; }
    public int? LocationId { get; set; }
    public int? ProductVariantId { get; set; }
    public string TransactionType { get; set; } = null!;
    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }
    public int? ReferenceItemId { get; set; }
    public int Quantity { get; set; }
    public int BeforeQuantity { get; set; }
    public int AfterQuantity { get; set; }
    public decimal? WeightKg { get; set; }
    public int? IotWeightLogId { get; set; }
    public string? Note { get; set; }

    public virtual Inventory Inventory { get; set; } = null!;
    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual Location? Location { get; set; }
    public virtual ProductVariant? ProductVariant { get; set; }
    public virtual IotWeightLog? IotWeightLog { get; set; }
}
