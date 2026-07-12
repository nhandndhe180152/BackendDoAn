using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class StockTakeItem : EntityAuditBase<int>
{
    public int StockTakeId { get; set; }
    public int? ProductVariantId { get; set; }
    public decimal SystemQuantity { get; set; }
    public int? LocationId { get; set; }
    public decimal? ActualQuantity { get; set; }
    public decimal Difference => (ActualQuantity ?? 0) - SystemQuantity;
    public string? Note { get; set; }
    public bool QRScanned { get; set; }

    public virtual StockTake StockTake { get; set; } = null!;
    public virtual ProductVariant? ProductVariant { get; set; }
    public virtual Location? Location { get; set; }
}
