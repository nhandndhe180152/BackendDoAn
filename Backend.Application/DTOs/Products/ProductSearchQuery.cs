using System;
using Backend.Share.Entities;

namespace Backend.Application.DTOs.Products;

public class ProductSearchQuery : SearchQuery
{
    public int? ProductCategoryId { get; set; }
    public bool? IsActive { get; set; }
    public bool IncludeDescendantCategories { get; set; } = false;
    public bool IncludeDeleted { get; set; } = false;
}

