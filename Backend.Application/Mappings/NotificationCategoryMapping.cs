using System;
using Backend.Application.DTOs.NotificationCategories;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class NotificationCategoryMapping
{
    public static NotificationCategory ToEntity(this CreateNotificationCategoryDto dto)
    {
        return new NotificationCategory
        {
            Color = dto.Color,
            Description = dto.Description,
            Name = dto.Name,
            CreatedDate = DateTime.Now,
            CreatedBy = dto.CreatedBy
        };
    }

    public static NotificationCategory ToEntity(this UpdateNotificationCategoryDto dto, NotificationCategory existData)
    {
        existData.Color = dto.Color;
        existData.Description = dto.Description;
        existData.Name = dto.Name;
        existData.UpdatedBy = dto.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;

        return existData;
    }

    public static NotificationCategoryDetailDto ToDto(this NotificationCategory entity)
    {
        return new NotificationCategoryDetailDto
        {
            Color = entity.Color,
            Description = entity.Description,
            CreatedDate = entity.CreatedDate,
            Id = entity.Id,
            Name = entity.Name,
        };
    }

    public static NotificationCategoryListDto ToListDto(this NotificationCategory entity)
    {
        return new NotificationCategoryListDto
        {
            Color = entity.Color,
            Description = entity.Description,
            CreatedDate = entity.CreatedDate,
            Id = entity.Id,
            Name = entity.Name,
        };
    }
}
