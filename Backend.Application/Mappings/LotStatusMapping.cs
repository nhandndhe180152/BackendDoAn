using System;
using Backend.Application.DTOs.LotStatuses;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class LotStatusMapping
{
    public static LotStatus ToEntity(this CreateLotStatusDto dto)
    {
        return new LotStatus
        {
            Code = dto.Code,
            Name = dto.Name,
            Color = dto.Color,
            IsSellable = dto.IsSellable,
            CreatedBy = dto.CreatedBy,
            CreatedDate = DateTime.Now,
            IsDeleted = false,
        };
    }

    public static LotStatus ToEntity(this UpdateLotStatusDto dto, LotStatus existData)
    {
        existData.Code = dto.Code;
        existData.Name = dto.Name;
        existData.Color = dto.Color;
        existData.IsSellable = dto.IsSellable;
        existData.UpdatedBy = dto.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;
        return existData;
    }
}
