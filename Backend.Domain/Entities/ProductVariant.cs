using System;
using System.Collections.Generic;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class ProductVariant : EntityCommonBase<int>
{
    public int ProductId { get; set; }
    public int UnitOfMeasureId { get; set; }
    public string SKU { get; set; } = null!;
    public string? QRCode { get; set; }
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal Weight { get; set; }
    public string? AttributeValues { get; set; }
    public int? ImageId { get; set; }
    public bool IsActive { get; set; }
    public decimal? MinStockLevel { get; set; }

    /// <summary>Giống lúa — dùng cho lúa nguyên liệu và gạo thành phẩm</summary>
    public int? RiceVarietyId { get; set; }

    /// <summary>true = phụ phẩm (tấm/cám/trấu); false = lúa hoặc gạo chính</summary>
    public bool IsByproduct { get; set; }

    public virtual Product Product { get; set; } = null!;
    public virtual UnitOfMeasure UnitOfMeasure { get; set; } = null!;
    public virtual FileUpload? Image { get; set; }
    public virtual RiceVariety? RiceVariety { get; set; }
}
