using System;

namespace Backend.Application.DTOs.StockTakes;

public class StockTakeItemDto
{
    public int Id { get; set; }
    public int StockTakeId { get; set; }
    public int? ProductVariantId { get; set; }
    public int? LocationId { get; set; }
    public decimal SystemQuantity { get; set; }
    public decimal? ActualQuantity { get; set; }
    public decimal Difference { get; set; }
    public string? Note { get; set; }
    public bool QRScanned { get; set; }
}

public class CreateStockTakeItemDto
{
    public int? ProductVariantId { get; set; }
    public int? LocationId { get; set; }
    public decimal SystemQuantity { get; set; }
    public decimal? ActualQuantity { get; set; }
    public string? Note { get; set; }
    public bool QRScanned { get; set; }
}

public class UpdateStockTakeItemDto : CreateStockTakeItemDto
{
    public int Id { get; set; }
}

