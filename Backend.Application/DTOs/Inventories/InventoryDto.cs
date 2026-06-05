using System;

namespace Backend.Application.DTOs.Inventories;

public class InventoryDto
{
    public int Id { get; set; }

    public int WarehouseId { get; set; }

    public string? WarehouseName { get; set; }

    public int? LocationId { get; set; }

    public string? LocationCode { get; set; }

    public int ProductVariantId { get; set; }

    public string? SKU { get; set; }

    public string? ProductVariantName { get; set; }

    public int ProductId { get; set; }

    public string? ProductName { get; set; }

    public int? PurchaseOrderId { get; set; }

    public decimal CostPrice { get; set; }

    public int QuantityOnHand { get; set; }

    public int QuantityReserved { get; set; }

    public int QuantityAvailable { get; set; }

    public decimal? MinStockLevel { get; set; }

    public bool IsLowStock { get; set; }

    public DateTime? LastStockTakeDate { get; set; }

    public DateTime? CreatedDate { get; set; }

    public DateTime? LastModifiedDate { get; set; }
}

