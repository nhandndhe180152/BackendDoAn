namespace Backend.Application.DTOs.ProductVariants;

public class SkuCheckResultDto
{
    public ProductVariantDetailDto ProductVariant { get; set; } = null!;
    
    // Current Stock Levels
    public int QuantityOnHand { get; set; }
    public int QuantityReserved { get; set; }
    public int QuantityAvailable { get; set; }
    
    // Transaction Document Slip Association
    public bool BelongsToDocument { get; set; }
    public int? DocumentQuantityOrdered { get; set; }
    public int? DocumentQuantityProcessed { get; set; } // QuantityReceived (PO), QuantityPicked (SO), or ActualQuantity (StockTake)
    public bool IsQrScanned { get; set; }
    
    public string Message { get; set; } = string.Empty;
}
