using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

/// <summary>
/// Dòng chi tiết điều chuyển nội bộ.
/// </summary>
public class StockTransferItem : EntityAuditBase<int>
{
    public int StockTransferId { get; set; }
    public int ProductVariantId { get; set; }
    public int? PaddyLotId { get; set; }
    public int? FromLocationId { get; set; }
    public int? ToLocationId { get; set; }
    public decimal WeightKg { get; set; }
    public string? Note { get; set; }

    // Navigation
    public virtual StockTransfer StockTransfer { get; set; } = null!;
    public virtual ProductVariant ProductVariant { get; set; } = null!;
    public virtual PaddyLot? PaddyLot { get; set; }
    public virtual Location? FromLocation { get; set; }
    public virtual Location? ToLocation { get; set; }
}
