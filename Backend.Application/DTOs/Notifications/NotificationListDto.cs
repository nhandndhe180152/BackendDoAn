using System;
using Backend.Share.Entities;

namespace Backend.Application.DTOs.Notifications;

public class NotificationListDto
{
    public int Id { get; set; }
    public int NotificationCategoryId { get; set; }
    public string NotificationCategoryName { get; set; } = null!;
    public string? DirectionId { get; set; }
    public string Title { get; set; } = null!;
    public string Content { get; set; } = null!;
    public int? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public List<DataItem<int>> Users { get; set; } = new List<DataItem<int>>();
}
