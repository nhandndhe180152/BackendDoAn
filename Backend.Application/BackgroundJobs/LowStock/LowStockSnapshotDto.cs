namespace Backend.Application.BackgroundJobs.LowStock;

public class LowStockSnapshotDto
{
    public int WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = null!;
    public int ProductVariantId { get; set; }
    public string SKU { get; set; } = null!;
    public string ProductName { get; set; } = null!;
    public decimal SellableOnHandKg { get; set; }
    public decimal ReservedKg { get; set; }
    public decimal AvailableKg { get; set; }
    public decimal ThresholdKg { get; set; }
}
