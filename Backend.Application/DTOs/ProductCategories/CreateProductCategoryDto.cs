using System;

namespace Backend.Application.DTOs.ProductCategories;

public class CreateProductCategoryDto
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int? ParentId { get; set; }
    public string TreeIds { get; set; } = null!;
    public int SortOrder { get; set; }
    public int? CreatedBy { get; set; }
}
