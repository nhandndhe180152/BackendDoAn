using System;

namespace Backend.Domain.Aggregates;

public class NotificationCategoryAggregate
{
    public int Id { get; set; } = 0;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string Color { get; set; } = null!;
    public DateTime CreatedDate { get; set; }
}
