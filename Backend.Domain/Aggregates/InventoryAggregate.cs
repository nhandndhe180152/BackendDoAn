using System;

namespace Backend.Domain.Aggregates;

public class InventoryAggregate
{
    public int Id { get; set; }

    public int WarehouseId { get; set; }

    public string WarehouseName { get; set; } = null!;

    public int? LocationId { get; set; }

    public string? LocationCode { get; set; }

    public int ProductVariantId { get; set; }

    public string SKU { get; set; } = null!;

    public string ProductVariantName { get; set; } = null!;

    public int ProductId { get; set; }

    public string ProductName { get; set; } = null!;

    public int? InboundOrderId { get; set; }

    public decimal CostPrice { get; set; }

    public int QuantityOnHand { get; set; }

    public int QuantityReserved { get; set; }

    public int QuantityAvailable { get; set; }

    public decimal? MinStockLevel { get; set; }

    public bool IsLowStock { get; set; }

    public DateTime? LastStockTakeDate { get; set; }

    public DateTime? CreatedDate { get; set; }
}
