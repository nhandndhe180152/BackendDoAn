using System;
using Backend.Application.DTOs.MillingOrderStatuses;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class MillingOrderStatusMapping
{
    public static MillingOrderStatus ToEntity(this CreateMillingOrderStatusDto dto)
    {
        return new MillingOrderStatus
        {
            Code = dto.Code,
            Name = dto.Name,
            Color = dto.Color,
            CreatedBy = dto.CreatedBy,
            CreatedDate = DateTime.Now,
            IsDeleted = false,
        };
    }

    public static MillingOrderStatus ToEntity(this UpdateMillingOrderStatusDto dto, MillingOrderStatus existData)
    {
        existData.Code = dto.Code;
        existData.Name = dto.Name;
        existData.Color = dto.Color;
        existData.UpdatedBy = dto.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;
        return existData;
    }
}
