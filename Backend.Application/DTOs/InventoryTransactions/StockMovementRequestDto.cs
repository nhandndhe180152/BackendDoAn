using System;

namespace Backend.Application.DTOs.InventoryTransactions;

public class StockMovementRequestDto
{
    public int ProductVariantId { get; set; }

    public int WarehouseId { get; set; }

    public int? LocationId { get; set; }

    public int Quantity { get; set; }

    public decimal? CostPrice { get; set; }

    public string? ReferenceType { get; set; }

    public int? ReferenceId { get; set; }

    public int? ReferenceItemId { get; set; }

    public decimal? WeightKg { get; set; }

    public int? IotWeightLogId { get; set; }

    public string? Note { get; set; }
}
