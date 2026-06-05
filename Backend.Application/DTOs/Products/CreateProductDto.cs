using System;

namespace Backend.Application.DTOs.Products;

public class CreateProductDto
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int ProductCategoryId { get; set; }
    public bool IsActive { get; set; }
    public int? CreatedBy { get; set; }
}
