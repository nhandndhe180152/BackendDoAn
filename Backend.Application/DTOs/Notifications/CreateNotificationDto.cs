using System;

namespace Backend.Application.DTOs.Notifications;

public class CreateNotificationDto
{
    public int NotificationCategoryId { get; set; }
    public string? DirectionId { get; set; }
    public string Title { get; set; } = null!;
    public string Content { get; set; } = null!;
    public int? CreatedBy { get; set; }
    public List<int> UserIds { get; set; } = new List<int>();
}
