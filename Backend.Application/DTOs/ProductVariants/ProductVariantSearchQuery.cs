using System;
using Backend.Share.Entities;

namespace Backend.Application.DTOs.ProductVariants;

public class ProductVariantSearchQuery : SearchQuery
{
    public int? ProductId { get; set; }
    public bool? IsActive { get; set; }
}
