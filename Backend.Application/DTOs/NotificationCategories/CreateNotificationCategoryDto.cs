using System;

namespace Backend.Application.DTOs.NotificationCategories;

public class CreateNotificationCategoryDto
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string Color { get; set; } = null!;
    public int? CreatedBy { get; set; }
}
