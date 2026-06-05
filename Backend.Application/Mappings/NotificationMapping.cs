using System;
using Backend.Application.DTOs.Notifications;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class NotificationMapping
{
    public static Notification ToEntity(this CreateNotificationDto obj)
    {
        return new Notification
        {
            NotificationCategoryId = obj.NotificationCategoryId,
            DirectionId = obj.DirectionId,
            Title = obj.Title,
            Content = obj.Content,
            CreatedBy = obj.CreatedBy,
            CreatedDate = DateTime.Now,
        };
    }

    public static Notification ToEntity(this UpdateNotificationDto obj, Notification existData)
    {
        existData.NotificationCategoryId = obj.NotificationCategoryId;
        existData.DirectionId = obj.DirectionId;
        existData.Title = obj.Title;
        existData.Content = obj.Content;
        existData.UpdatedBy = obj.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;
        return existData;
    }

    public static NotificationDetailDto ToDto(this Notification entity)
    {
        return new NotificationDetailDto
        {
            Id = entity.Id,
            NotificationCategoryId = entity.NotificationCategoryId,
            Title = entity.Title,
            Content = entity.Content,
            CreatedDate = entity.CreatedDate,
        };
    }
}
