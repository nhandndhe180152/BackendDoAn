using System;

namespace Backend.Application.DTOs.ProductAttributes;

public class UpdateProductAttributeDto : CreateProductAttributeDto
{
    public int Id { get; set; }
    public int? UpdatedBy { get; set; }
}
