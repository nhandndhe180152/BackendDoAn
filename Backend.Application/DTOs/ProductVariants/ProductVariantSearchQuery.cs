using System;
using Backend.Share.Entities;

namespace Backend.Application.DTOs.ProductVariants;

public class ProductVariantSearchQuery : SearchQuery
{
    public string? Sku { get; set; }
    public string? QrCode { get; set; }
    public int? ProductId { get; set; }
    public int? ProductCategoryId { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsIoTRequired { get; set; }
    public bool? HasMinStockLevel { get; set; }
    public bool IncludeDeleted { get; set; } = false;
}

