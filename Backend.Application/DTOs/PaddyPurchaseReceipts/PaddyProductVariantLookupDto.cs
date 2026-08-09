namespace Backend.Application.DTOs.PaddyPurchaseReceipts;

/// <summary>
/// Item cho dropdown "Sản phẩm" trên phiếu mua lúa. FE lọc theo RiceVarietyId của giống đã chọn.
/// </summary>
public class PaddyProductVariantLookupDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Sku { get; set; }
    public int? RiceVarietyId { get; set; }
    public string? RiceVarietyName { get; set; }
    public bool IsActive { get; set; }
}
