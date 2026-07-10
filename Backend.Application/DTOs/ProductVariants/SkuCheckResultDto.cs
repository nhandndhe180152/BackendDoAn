namespace Backend.Application.DTOs.ProductVariants;

public class SkuCheckResultDto
{
    public ProductVariantDetailDto ProductVariant { get; set; } = null!;
    
    // Current Stock Levels
    public decimal QuantityOnHand { get; set; }
    public decimal QuantityReserved { get; set; }
    public decimal QuantityAvailable { get; set; }
    
    // Transaction Document Slip Association
    public bool BelongsToDocument { get; set; }
    public decimal? DocumentQuantityOrdered { get; set; }
    public decimal? DocumentQuantityProcessed { get; set; } // QuantityReceived (PO), QuantityPicked (SO), or ActualQuantity (StockTake)
    public bool IsQrScanned { get; set; }
    
    public string Message { get; set; } = string.Empty;
}
