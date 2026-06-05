using System;

namespace Backend.Application.DTOs.ProductAttributes;

public class CreateProductAttributeDto
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int? CreatedBy { get; set; }
}
