using System;

namespace Backend.Application.DTOs.NotificationCategories;

public class UpdateNotificationCategoryDto : CreateNotificationCategoryDto
{
    public int Id { get; set; } = 0;
    public int? UpdatedBy { get; set; }
}
