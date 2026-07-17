using System;

namespace Backend.Application.DTOs.StockAlertConfigs;

public class StockAlertConfigDetailDto
{
    public int Id { get; set; }
    public int WarehouseId { get; set; }
    public string? WarehouseCode { get; set; }
    public string? WarehouseName { get; set; }
    public int? ProductVariantId { get; set; }
    public string? ProductVariantSku { get; set; }
    public string? ProductName { get; set; }
    public int MinThreshold { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }
}
