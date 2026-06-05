using System;
using Backend.Share.Entities;

namespace Backend.Application.DTOs.Notifications;

public class NotificationDetailDto
{
    public int Id { get; set; }
    public int NotificationCategoryId { get; set; }
    public string? DirectionId { get; set; }
    public string Title { get; set; } = null!;
    public string Content { get; set; } = null!;
    public int? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public DataItem<int> NotificationCategory { get; set; } = new DataItem<int>();
    public List<DataItem<int>> NotificationUsers { get; set; } = new List<DataItem<int>>();
}
