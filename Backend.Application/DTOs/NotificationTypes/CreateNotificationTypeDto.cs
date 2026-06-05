using System;

namespace Backend.Application.DTOs.NotificationTypes;

public class CreateNotificationTypeDto
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int? CreatedBy { get; set; }
}
