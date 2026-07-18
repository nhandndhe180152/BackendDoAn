using System;

namespace Backend.Application.DTOs.InventoryTransactions;

public class StockMovementRequestDto
{
    public int ProductVariantId { get; set; }

    public int WarehouseId { get; set; }

    public int? LocationId { get; set; }

    /// ID lô hàng. Điền khi di chuyển hàng có lô để khớp đúng dòng Inventory theo lot-centric unique index.
    public int? PaddyLotId { get; set; }

    public decimal Quantity { get; set; }

    public decimal? CostPrice { get; set; }

    public string? ReferenceType { get; set; }

    public int? ReferenceId { get; set; }

    public int? ReferenceItemId { get; set; }

    public decimal? WeightKg { get; set; }

    public string? Note { get; set; }
}
