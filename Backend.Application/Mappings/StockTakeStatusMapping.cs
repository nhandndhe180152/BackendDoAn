using System;
using Backend.Application.DTOs.StockTakeStatuses;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class StockTakeStatusMapping
{
    public static StockTakeStatus ToEntity(this CreateStockTakeStatusDto dto)
    {
        return new StockTakeStatus
        {
            Code = dto.Code,
            Name = dto.Name,
            Color = dto.Color,
            CreatedBy = dto.CreatedBy,
            CreatedDate = DateTime.Now,
            IsDeleted = false,
        };
    }

    public static StockTakeStatus ToEntity(this UpdateStockTakeStatusDto dto, StockTakeStatus existData)
    {
        existData.Code = dto.Code;
        existData.Name = dto.Name;
        existData.Color = dto.Color;
        existData.UpdatedBy = dto.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;
        return existData;
    }
}
