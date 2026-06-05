using System;

namespace Backend.Application.DTOs.ProductVariants;

public class ProductVariantDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int ProductId { get; set; }
    public string? ProductName { get; set; }
    public int UnitOfMeasureId { get; set; }
    public string? UnitOfMeasureName { get; set; }
    public string SKU { get; set; } = null!;
    public string? QRCode { get; set; }
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal Weight { get; set; }
    public string? AttributeValues { get; set; }
    public int? ImageId { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; }
    public decimal? MinStockLevel { get; set; }
    public DateTime CreatedDate { get; set; }
}
