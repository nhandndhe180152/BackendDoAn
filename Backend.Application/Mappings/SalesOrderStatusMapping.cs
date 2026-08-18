using System;
using Backend.Application.DTOs.SalesOrderStatuses;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class SalesOrderStatusMapping
{
    public static SalesOrderStatus ToEntity(this CreateSalesOrderStatusDto dto)
    {
        return new SalesOrderStatus
        {
            Code = dto.Code.Trim().ToUpperInvariant(),
            Name = dto.Name.Trim(),
            Color = dto.Color.Trim(),
            CreatedBy = dto.CreatedBy,
            CreatedDate = DateTime.Now,
            IsDeleted = false,
        };
    }

    public static SalesOrderStatus ToEntity(this UpdateSalesOrderStatusDto dto, SalesOrderStatus existData)
    {
        existData.Name = dto.Name.Trim();
        existData.Color = dto.Color.Trim();
        existData.UpdatedBy = dto.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;
        return existData;
    }
}
