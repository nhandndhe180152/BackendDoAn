using System;
using Backend.Application.DTOs.Provinces;
using Backend.Domain.Entities;
using Backend.Share.Helpers;

namespace Backend.Application.Mappings;

public static class ProvinceMapping
{
    public static Province ToEntity(this CreateProvinceDto obj)
    {
        return new Province
        {
            Code = obj.Code,
            Slug = StringHelper.Slugify(obj.Name),
            Type = obj.Type,
            IsCentral = obj.IsCentral,
            CreatedBy = obj.CreatedBy,
            CreatedDate = DateTime.Now,
            Name = obj.Name,
        };
    }

    public static Province ToEntity(this UpdateProvinceDto obj, Province existData)
    {
        existData.UpdatedBy = obj.UpdatedBy;
        existData.Code = obj.Code;
        existData.Name = obj.Name;
        existData.LastModifiedDate = DateTime.Now;
        existData.Slug = StringHelper.Slugify(obj.Name);
        existData.Type = obj.Type;
        existData.IsCentral = obj.IsCentral;
        return existData;
    }

    public static ProvinceDetailDto ToDto(this Province entity)
    {
        return new ProvinceDetailDto
        {
            Id = entity.Id,
            CreatedDate = entity.CreatedDate,
            Name = entity.Name,
            Code = entity.Code,
            Slug = entity.Slug,
            Type = entity.Type,
            IsCentral = entity.IsCentral,
        };
    }
}
