using System;

namespace Backend.Application.DTOs.NotificationTypes;

public class UpdateNotificationTypeDto : CreateNotificationTypeDto
{
    public int Id { get; set; }
    public int? UpdatedBy { get; set; }
}
