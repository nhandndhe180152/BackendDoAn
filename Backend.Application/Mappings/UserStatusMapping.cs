using System;
using Backend.Application.DTOs.UserStatuses;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class UserStatusMapping
{
    public static UserStatus ToEntity(this CreateUserStatusDto dto)
    {
        return new UserStatus
        {
            Name = dto.Name,
            Description = dto.Description,
            Color = dto.Color,
            CreatedBy = dto.CreatedBy,
            CreatedDate = DateTime.Now,
            IsDeleted = false,
        };
    }
    public static UserStatus ToEntity(this UpdateUserStatusDto dto, UserStatus existData)
    {
        existData.Color = dto.Color;
        existData.Name = dto.Name;
        existData.Description = dto.Description;
        existData.UpdatedBy = dto.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;
        return existData;
    }
}