using System;
using Backend.Application.DTOs.NotificationTypes;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class NotificationTypeMapping
{
    public static NotificationType ToEntity(this CreateNotificationTypeDto obj)
    {
        return new NotificationType
        {
            CreatedBy = obj.CreatedBy,
            Description = obj.Description,
            Name = obj.Name,
            CreatedDate = DateTime.Now
        };
    }

    public static NotificationType ToEntity(this UpdateNotificationTypeDto obj, NotificationType existData)
    {
        existData.UpdatedBy = obj.UpdatedBy;
        existData.Description = obj.Description;
        existData.Name = obj.Name;
        existData.LastModifiedDate = DateTime.Now;

        return existData;
    }

    public static NotificationTypeDetailDto ToDto(this NotificationType entity)
    {
        return new NotificationTypeDetailDto
        {
            Id = entity.Id,
            CreatedDate = entity.CreatedDate,
            Description = entity.Description,
            Name = entity.Name,
        };
    }
}
