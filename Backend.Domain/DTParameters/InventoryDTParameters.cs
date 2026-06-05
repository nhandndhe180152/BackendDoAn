using System;
using Backend.Share.Entities;

namespace Backend.Domain.DTParameters;

public class InventoryDTParameters : DTParameter
{
    public int? WarehouseId { get; set; }

    public int? LocationId { get; set; }

    public int? ProductVariantId { get; set; }

    public bool? LowStockOnly { get; set; }
}
