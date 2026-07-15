using System;
using System.Collections.Generic;

namespace Backend.Application.DTOs.StockTransfers;

public class StockTransferItemDto
{
    public int ProductVariantId { get; set; }
    public int? PaddyLotId { get; set; }
    public int? FromLocationId { get; set; }
    public int? ToLocationId { get; set; }
    public decimal WeightKg { get; set; }
    public string? Note { get; set; }
}

public class CreateStockTransferDto
{
    public int? OrganizationId { get; set; }
    public int FromWarehouseId { get; set; }
    public int ToWarehouseId { get; set; }
    public DateTime TransferDate { get; set; }
    public int? AssignedUserId { get; set; }
    public string? Note { get; set; }
    public List<StockTransferItemDto> Items { get; set; } = new();
    public int? CreatedBy { get; set; }
}
