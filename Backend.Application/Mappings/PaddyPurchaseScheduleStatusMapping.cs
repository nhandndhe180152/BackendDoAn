using System;
using Backend.Application.DTOs.PaddyPurchaseScheduleStatuses;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class PaddyPurchaseScheduleStatusMapping
{
    public static PaddyPurchaseScheduleStatus ToEntity(this CreatePaddyPurchaseScheduleStatusDto dto)
    {
        return new PaddyPurchaseScheduleStatus
        {
            Code = dto.Code,
            Name = dto.Name,
            Color = dto.Color,
            CreatedBy = dto.CreatedBy,
            CreatedDate = DateTime.Now,
            IsDeleted = false,
        };
    }

    public static PaddyPurchaseScheduleStatus ToEntity(this UpdatePaddyPurchaseScheduleStatusDto dto, PaddyPurchaseScheduleStatus existData)
    {
        existData.Code = dto.Code;
        existData.Name = dto.Name;
        existData.Color = dto.Color;
        existData.UpdatedBy = dto.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;
        return existData;
    }
}
