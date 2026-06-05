using System.Collections.Generic;

namespace Backend.Application.DTOs.ProductVariants;

public class BatchQRLabelRequestDto
{
    public List<ProductVariantQuantityDto> Items { get; set; } = new();
    public float WidthMm { get; set; } = 50f;
    public float HeightMm { get; set; } = 30f;
}

public class ProductVariantQuantityDto
{
    public int ProductVariantId { get; set; }
    public int Quantity { get; set; } = 1;
}
