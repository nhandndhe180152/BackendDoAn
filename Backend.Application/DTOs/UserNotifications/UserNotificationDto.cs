using System;

namespace Backend.Application.DTOs.UserNotifications;

public class UserNotificationDto
{
    public int Id { get; set; }
    public int NotificationId { get; set; }
    public string Title { get; set; } = null!;
    public string Content { get; set; } = null!;
    public string? DirectionId { get; set; }
    public int NotificationCategoryId { get; set; }
    public string NotificationCategoryName { get; set; } = null!;
    public bool IsRead { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
