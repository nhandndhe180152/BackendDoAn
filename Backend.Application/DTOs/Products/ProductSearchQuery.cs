using System;
using Backend.Share.Entities;

namespace Backend.Application.DTOs.Products;

public class ProductSearchQuery : SearchQuery
{
    public int? ProductCategoryId { get; set; }
    public bool? IsActive { get; set; }
}
