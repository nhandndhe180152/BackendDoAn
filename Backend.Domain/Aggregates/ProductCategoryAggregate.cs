using System;

namespace Backend.Domain.Aggregates;

public class ProductCategoryAggregate
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int? ParentId { get; set; }
    public string? ParentName { get; set; }
    public string TreeIds { get; set; } = null!;
    public int SortOrder { get; set; }
    public DateTime CreatedDate { get; set; }

    /// Số lượng sản phẩm thuộc danh mục này
    public int ProductCount { get; set; }
}
