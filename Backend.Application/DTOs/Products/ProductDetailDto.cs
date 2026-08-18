using System;

namespace Backend.Application.DTOs.Products;

public class ProductDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int ProductCategoryId { get; set; }
    public string? ProductCategoryName { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
    public int VariantCount { get; set; }
    public int ActiveVariantCount { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }
}

