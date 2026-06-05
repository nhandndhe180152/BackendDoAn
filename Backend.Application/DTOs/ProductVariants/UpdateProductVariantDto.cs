using System;

namespace Backend.Application.DTOs.ProductVariants;

public class UpdateProductVariantDto : CreateProductVariantDto
{
    public int Id { get; set; }
    public int? UpdatedBy { get; set; }
}
