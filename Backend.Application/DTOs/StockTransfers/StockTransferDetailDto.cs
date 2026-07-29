using System;
using System.Collections.Generic;

namespace Backend.Application.DTOs.StockTransfers;

public class StockTransferDetailDto
{
    public int Id { get; set; }
    public int? OrganizationId { get; set; }
    public string TransferCode { get; set; } = null!;
    public int StatusId { get; set; }
    public string? StatusName { get; set; }
    public string? StatusColor { get; set; }
    public int FromWarehouseId { get; set; }
    public string? FromWarehouseName { get; set; }
    public int ToWarehouseId { get; set; }
    public string? ToWarehouseName { get; set; }
    public int? AssignedUserId { get; set; }
    public DateTime TransferDate { get; set; }
    public string? Note { get; set; }
    public int ItemCount { get; set; }
    public decimal TotalWeightKg { get; set; }
    public List<StockTransferItemDetailDto> Items { get; set; } = new();
    public DateTime CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }
}

public class StockTransferItemDetailDto
{
    public int Id { get; set; }
    public int ProductVariantId { get; set; }
    public string? SKU { get; set; }
    public string? ProductVariantName { get; set; }
    public int? PaddyLotId { get; set; }
    public string? LotCode { get; set; }
    public int? FromLocationId { get; set; }
    public string? FromLocationName { get; set; }
    public int? ToLocationId { get; set; }
    public string? ToLocationName { get; set; }
    public decimal WeightKg { get; set; }
    public string? Note { get; set; }
}

public class StockTransferSummaryDto
{
    public int TransfersThisMonth { get; set; }
    public int InTransitCount { get; set; }
    public decimal TotalTransferredWeightKg { get; set; }
}

public class CancelStockTransferDto
{
    public string? Reason { get; set; }
}
