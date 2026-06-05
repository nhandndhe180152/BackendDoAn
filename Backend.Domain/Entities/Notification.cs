using System;
using System.Text.Json.Serialization;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class Notification : EntityAuditBase<int>
{
    public int NotificationCategoryId { get; set; }
    public int NotificationTypeId { get; set; }
    public string? DirectionId { get; set; }
    public string Title { get; set; } = null!;
    public string Content { get; set; } = null!;
    [JsonIgnore]
    public virtual NotificationCategory NotificationCategory { get; set; }
    [JsonIgnore]
    public virtual NotificationType NotificationType { get; set; }
    [JsonIgnore]
    public virtual ICollection<UserNotification> UserNotifications { get; set; }
}
