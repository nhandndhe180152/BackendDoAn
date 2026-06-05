using System;
using System.Collections.Generic;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class ProductCategory : EntityCommonBase<int>
{
    public int? ParentId { get; set; }
    public string TreeIds { get; set; } = null!;
    public int SortOrder { get; set; }

    public virtual ProductCategory? ParentCategory { get; set; }
    public virtual ICollection<ProductCategory> SubCategories { get; set; } = new List<ProductCategory>();
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
