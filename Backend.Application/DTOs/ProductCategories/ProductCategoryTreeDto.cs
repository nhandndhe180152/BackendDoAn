using System;
using System.Collections.Generic;

namespace Backend.Application.DTOs.ProductCategories;

/// Represents a category node for the tree endpoint
public class ProductCategoryTreeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int? ParentCategoryId { get; set; }
    public string TreeIds { get; set; } = null!;
    public int SortOrder { get; set; }
    public int ProductCount { get; set; }
    public List<ProductCategoryTreeDto> Children { get; set; } = new();
}
