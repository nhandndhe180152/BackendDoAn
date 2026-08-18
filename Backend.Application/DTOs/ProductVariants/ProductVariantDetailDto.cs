using System;
using System.Collections.Generic;

namespace Backend.Application.DTOs.ProductVariants;

public class ProductVariantDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int ProductId { get; set; }
    public string? ProductName { get; set; }
    public bool ProductIsActive { get; set; }
    public int? ProductCategoryId { get; set; }
    public string? ProductCategoryName { get; set; }
    public int UnitOfMeasureId { get; set; }
    public string? UnitOfMeasureName { get; set; }
    public string SKU { get; set; } = null!;
    public string? QRCode { get; set; }
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    /// Khối lượng bao chuẩn theo kg (0 = không áp dụng/chưa cấu hình). Giữ tên Weight để tương thích API hiện tại.
    public decimal Weight { get; set; }
    public int? ImageId { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
    public decimal? MinStockLevel { get; set; }
    public int? RiceVarietyId { get; set; }
    public bool IsByproduct { get; set; }

    /// Parsed structured attributes when AttributeValues is valid JSON
    public List<AttributeValueDto>? AttributeValuesJson { get; set; }
    /// Raw stored value when AttributeValues is not valid JSON (legacy plain text)
    public string? LegacyAttributeValues { get; set; }

    /// True only when: Variant.IsActive && !Variant.IsDeleted && Product.IsActive && !Product.IsDeleted && !Category.IsDeleted
    public bool EffectiveActiveStatus { get; set; }

    public DateTime CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }
}

