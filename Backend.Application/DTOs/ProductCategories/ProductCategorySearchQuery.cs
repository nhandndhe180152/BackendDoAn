using System;
using Backend.Share.Entities;

namespace Backend.Application.DTOs.ProductCategories;

public class ProductCategorySearchQuery : SearchQuery
{
    public int? ParentId { get; set; }
}
