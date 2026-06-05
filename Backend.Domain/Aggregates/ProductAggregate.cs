using System;

namespace Backend.Domain.Aggregates;

public class ProductAggregate
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int ProductCategoryId { get; set; }
    public string ProductCategoryName { get; set; } = null!;
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
}
