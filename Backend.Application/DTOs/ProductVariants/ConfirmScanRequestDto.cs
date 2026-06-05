namespace Backend.Application.DTOs.ProductVariants;

public class ConfirmScanRequestDto
{
    public string Sku { get; set; } = null!;
    public string DocumentType { get; set; } = null!; // "PurchaseOrder", "SalesOrder", "StockTake"
    public int DocumentId { get; set; }
    public int Quantity { get; set; } = 1;
}
