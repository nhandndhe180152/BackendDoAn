using System;

namespace Backend.Application.DTOs.Products;

public class UpdateProductDto : CreateProductDto
{
    public int Id { get; set; }
    public int? UpdatedBy { get; set; }
}
