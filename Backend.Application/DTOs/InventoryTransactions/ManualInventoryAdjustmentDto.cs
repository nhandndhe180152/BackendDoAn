using System;

namespace Backend.Application.DTOs.InventoryTransactions;

public class ManualInventoryAdjustmentDto
{
    public int ProductVariantId { get; set; }

    public int WarehouseId { get; set; }

    public int? LocationId { get; set; }

    public int? NewQuantityOnHand { get; set; }

    public int? AdjustmentQuantity { get; set; }

    public string Reason { get; set; } = null!;
}
