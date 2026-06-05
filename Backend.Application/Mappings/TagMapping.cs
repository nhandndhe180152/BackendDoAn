using System;
using Backend.Application.DTOs.Tags;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class TagMapping
{
    public static Tag ToEntity(this CreateTagDto obj)
    {
        return new Tag
        {
            TagTypeId = obj.TagTypeId,
            CreatedBy = obj.CreatedBy,
            Description = obj.Description,
            Name = obj.Name,
            CreatedDate = DateTime.Now
        };
    }

    public static Tag ToEntity(this UpdateTagDto obj, Tag existData)
    {
        existData.UpdatedBy = obj.UpdatedBy;
        existData.Description = obj.Description;
        existData.Name = obj.Name;
        existData.LastModifiedDate = DateTime.Now;
        existData.TagTypeId = obj.TagTypeId;
        return existData;
    }

    public static TagDetailDto ToDto(this Tag entity)
    {
        return new TagDetailDto
        {
            Id = entity.Id,
            CreatedDate = entity.CreatedDate,
            Description = entity.Description,
            Name = entity.Name,
            TagTypeId = entity.TagTypeId,
            TagTypeName = entity.TagType?.Name ?? string.Empty
        };
    }
}
