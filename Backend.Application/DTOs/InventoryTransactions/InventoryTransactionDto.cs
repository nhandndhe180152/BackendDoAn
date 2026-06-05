using System;

namespace Backend.Application.DTOs.InventoryTransactions;

public class InventoryTransactionDto
{
    public int Id { get; set; }

    public int InventoryId { get; set; }

    public int WarehouseId { get; set; }

    public string? WarehouseName { get; set; }

    public int? LocationId { get; set; }

    public string? LocationCode { get; set; }

    public int? ProductVariantId { get; set; }

    public string? SKU { get; set; }

    public string? ProductVariantName { get; set; }

    public string? ProductName { get; set; }

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

    public DateTime CreatedDate { get; set; }

    public int? CreatedBy { get; set; }
}
