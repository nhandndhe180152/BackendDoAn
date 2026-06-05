using System;
using System.Collections.Generic;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class Product : EntityCommonBase<int>
{
    public int ProductCategoryId { get; set; }
    public bool IsActive { get; set; }

    public virtual ProductCategory ProductCategory { get; set; } = null!;
    public virtual ICollection<ProductVariant> ProductVariants { get; set; } = new List<ProductVariant>();
}
