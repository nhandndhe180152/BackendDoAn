using System;
using Backend.Application.DTOs.SystemConfigs;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class SystemConfigMapping
{
    public static SystemConfig ToEntity(this CreateSystemConfigDto obj)
    {
        return new SystemConfig
        {
            Name = obj.Name,
            //ConfigKey = StringHelper.Slugify(obj.ConfigKey).ToUpper(),
            ConfigKey = obj.ConfigKey,
            ConfigValue = obj.ConfigValue,
            CreatedBy = obj.CreatedBy,
            CreatedDate = DateTime.Now,
            Description = obj.Description,
        };
    }

    public static SystemConfig ToEntity(this UpdateSystemConfigDto obj, SystemConfig existData)
    {
        existData.Name = obj.Name;
        //existData.ConfigKey = StringHelper.Slugify(obj.ConfigKey).ToUpper();
        existData.ConfigKey = obj.ConfigKey;
        existData.ConfigValue = obj.ConfigValue;
        existData.Description = obj.Description;
        existData.UpdatedBy = obj.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;

        return existData;
    }

    public static SystemConfigDetailDto ToDto(this SystemConfig obj)
    {
        return new SystemConfigDetailDto
        {
            Id = obj.Id,
            ConfigKey = obj.ConfigKey,
            ConfigValue = obj.ConfigValue,
            CreatedDate = obj.CreatedDate,
            Description = obj.Description,
            Name = obj.Name
        };
    }
}
