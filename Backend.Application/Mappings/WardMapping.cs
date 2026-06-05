using System;
using Backend.Application.DTOs.Wards;
using Backend.Domain.Entities;
using Backend.Share.Helpers;

namespace Backend.Application.Mappings;

public static class WardMapping
{
    public static Ward ToEntity(this CreateWardDto obj)
    {
        return new Ward
        {
            Code = obj.Code,
            Slug = StringHelper.Slugify(obj.Name),
            Type = obj.Type,
            ProvinceId = (int)obj.ProvinceId!,
            CreatedBy = obj.CreatedBy,
            CreatedDate = DateTime.Now,
            Name = obj.Name,
            ProvinceCode = obj.ProvinceCode,
        };
    }

    public static Ward ToEntity(this UpdateWardDto obj, Ward existData)
    {
        existData.UpdatedBy = obj.UpdatedBy;
        existData.Code = obj.Code;
        existData.Name = obj.Name;
        existData.LastModifiedDate = DateTime.Now;
        existData.Slug = StringHelper.Slugify(obj.Name);
        existData.Type = obj.Type;
        existData.ProvinceId = (int)obj.ProvinceId!;
        existData.ProvinceCode = obj.ProvinceCode;
        return existData;
    }

    public static WardDetailDto ToDto(this Ward entity)
    {
        return new WardDetailDto
        {
            Id = entity.Id,
            CreatedDate = entity.CreatedDate,
            Name = entity.Name,
            Code = entity.Code,
            Slug = entity.Slug,
            Type = entity.Type,
            ProvinceId = entity.ProvinceId,
            ProvinceCode = entity.ProvinceCode,
        };
    }
}
