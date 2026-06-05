using System;

namespace Backend.Domain.Aggregates;

public class NotificationAggregate
{
    public int Id { get; set; }
    public int NotificationCategoryId { get; set; }
    public string NotificationCategoryName { get; set; } = null!;
    public string NotificationCategoryColor { get; set; } = null!;
    public string? DirectionId { get; set; }
    public string Title { get; set; } = null!;
    public string Content { get; set; } = null!;
    public DateTime CreatedDate { get; set; }
}
