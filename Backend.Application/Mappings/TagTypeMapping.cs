using System;
using Backend.Application.DTOs.TagTypes;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class TagTypeMapping
{
    public static TagType ToEntity(this CreateTagTypeDto obj)
    {
        return new TagType
        {
            CreatedBy = obj.CreatedBy,
            Description = obj.Description,
            Name = obj.Name,
            CreatedDate = DateTime.Now
        };
    }

    public static TagType ToEntity(this UpdateTagTypeDto obj, TagType existData)
    {
        existData.UpdatedBy = obj.UpdatedBy;
        existData.Description = obj.Description;
        existData.Name = obj.Name;
        existData.LastModifiedDate = DateTime.Now;

        return existData;
    }

    public static TagTypeDetailDto ToDto(this TagType entity)
    {
        return new TagTypeDetailDto
        {
            Id = entity.Id,
            CreatedDate = entity.CreatedDate,
            Description = entity.Description,
            Name = entity.Name,
        };
    }
}
