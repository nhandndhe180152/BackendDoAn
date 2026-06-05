using System;

namespace Backend.Application.DTOs.Notifications;

public class UpdateNotificationDto : CreateNotificationDto
{
    public int Id { get; set; }
    public int? UpdatedBy { get; set; }
}
