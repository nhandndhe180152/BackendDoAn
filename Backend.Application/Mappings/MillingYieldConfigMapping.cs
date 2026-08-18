using System;
using Backend.Application.DTOs.MillingYieldConfigs;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class MillingYieldConfigMapping
{
    public static MillingYieldConfig ToEntity(this CreateMillingYieldConfigDto obj)
    {
        return new MillingYieldConfig
        {
            OrganizationId = obj.OrganizationId,
            RiceVarietyId = obj.RiceVarietyId,
            MoistureFrom = obj.MoistureFrom,
            MoistureTo = obj.MoistureTo,
            YieldRate = obj.YieldRate,
            BrokenRiceRate = obj.BrokenRiceRate,
            BranRate = obj.BranRate,
            HuskRate = obj.HuskRate,
            EffectiveFrom = obj.EffectiveFrom,
            IsActive = obj.IsActive,
            CreatedBy = obj.CreatedBy,
            CreatedDate = DateTime.Now
        };
    }

    public static MillingYieldConfig ToEntity(this UpdateMillingYieldConfigDto obj, MillingYieldConfig existData)
    {
        existData.OrganizationId = obj.OrganizationId;
        existData.RiceVarietyId = obj.RiceVarietyId;
        existData.MoistureFrom = obj.MoistureFrom;
        existData.MoistureTo = obj.MoistureTo;
        existData.YieldRate = obj.YieldRate;
        existData.BrokenRiceRate = obj.BrokenRiceRate;
        existData.BranRate = obj.BranRate;
        existData.HuskRate = obj.HuskRate;
        existData.EffectiveFrom = obj.EffectiveFrom;
        existData.IsActive = obj.IsActive;
        existData.UpdatedBy = obj.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;
        return existData;
    }

    public static MillingYieldConfigDetailDto ToDto(this MillingYieldConfig entity)
    {
        return new MillingYieldConfigDetailDto
        {
            Id = entity.Id,
            OrganizationId = entity.OrganizationId,
            RiceVarietyId = entity.RiceVarietyId,
            RiceVarietyCode = entity.RiceVariety?.Code,
            RiceVarietyName = entity.RiceVariety?.Name,
            MoistureFrom = entity.MoistureFrom,
            MoistureTo = entity.MoistureTo,
            YieldRate = entity.YieldRate,
            BrokenRiceRate = entity.BrokenRiceRate,
            BranRate = entity.BranRate,
            HuskRate = entity.HuskRate,
            EffectiveFrom = entity.EffectiveFrom,
            IsActive = entity.IsActive,
            CreatedDate = entity.CreatedDate,
            LastModifiedDate = entity.LastModifiedDate
        };
    }
}
