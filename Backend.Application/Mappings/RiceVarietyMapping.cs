using System;
using Backend.Application.DTOs.RiceVarieties;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class RiceVarietyMapping
{
    public static RiceVariety ToEntity(this CreateRiceVarietyDto obj)
    {
        return new RiceVariety
        {
            OrganizationId = obj.OrganizationId,
            Code = obj.Code.Trim(),
            Name = obj.Name.Trim(),
            Season = obj.Season?.Trim(),
            DefaultYieldRate = obj.DefaultYieldRate,
            Note = obj.Note?.Trim(),
            IsActive = obj.IsActive,
            CreatedBy = obj.CreatedBy,
            CreatedDate = DateTime.Now
        };
    }

    public static RiceVariety ToEntity(this UpdateRiceVarietyDto obj, RiceVariety existData)
    {
        existData.OrganizationId = obj.OrganizationId;
        existData.Code = obj.Code.Trim();
        existData.Name = obj.Name.Trim();
        existData.Season = obj.Season?.Trim();
        existData.DefaultYieldRate = obj.DefaultYieldRate;
        existData.Note = obj.Note?.Trim();
        existData.IsActive = obj.IsActive;
        existData.UpdatedBy = obj.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;
        return existData;
    }

    public static RiceVarietyDetailDto ToDto(this RiceVariety entity)
    {
        return new RiceVarietyDetailDto
        {
            Id = entity.Id,
            OrganizationId = entity.OrganizationId,
            Code = entity.Code,
            Name = entity.Name,
            Season = entity.Season,
            DefaultYieldRate = entity.DefaultYieldRate,
            Note = entity.Note,
            IsActive = entity.IsActive,
            CreatedDate = entity.CreatedDate,
            LastModifiedDate = entity.LastModifiedDate
        };
    }
}
