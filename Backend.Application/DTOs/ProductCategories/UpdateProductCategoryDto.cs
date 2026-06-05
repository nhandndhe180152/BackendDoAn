using System;

namespace Backend.Application.DTOs.ProductCategories;

public class UpdateProductCategoryDto : CreateProductCategoryDto
{
    public int Id { get; set; }
    public int? UpdatedBy { get; set; }
}
