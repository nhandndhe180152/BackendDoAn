using System;
using Backend.Application.DTOs.StockTransferStatuses;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class StockTransferStatusMapping
{
    public static StockTransferStatus ToEntity(this CreateStockTransferStatusDto dto)
    {
        return new StockTransferStatus
        {
            Code = dto.Code.Trim().ToUpperInvariant(),
            Name = dto.Name.Trim(),
            Color = dto.Color.Trim(),
            CreatedBy = dto.CreatedBy,
            CreatedDate = DateTime.Now,
            IsDeleted = false,
        };
    }

    public static StockTransferStatus ToEntity(this UpdateStockTransferStatusDto dto, StockTransferStatus existData)
    {
        existData.Name = dto.Name.Trim();
        existData.Color = dto.Color.Trim();
        existData.UpdatedBy = dto.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;
        return existData;
    }
}
