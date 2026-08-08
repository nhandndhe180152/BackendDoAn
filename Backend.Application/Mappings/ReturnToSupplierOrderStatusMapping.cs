using System;
using Backend.Application.DTOs.ReturnToSupplierOrderStatuses;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class ReturnToSupplierOrderStatusMapping
{
    public static ReturnToSupplierOrderStatus ToEntity(this CreateReturnToSupplierOrderStatusDto dto)
    {
        return new ReturnToSupplierOrderStatus
        {
            Code = dto.Code.Trim().ToUpperInvariant(),
            Name = dto.Name.Trim(),
            Color = dto.Color.Trim(),
            CreatedBy = dto.CreatedBy,
            CreatedDate = DateTime.Now,
            IsDeleted = false,
        };
    }

    public static ReturnToSupplierOrderStatus ToEntity(this UpdateReturnToSupplierOrderStatusDto dto, ReturnToSupplierOrderStatus existData)
    {
        existData.Name = dto.Name.Trim();
        existData.Color = dto.Color.Trim();
        existData.UpdatedBy = dto.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;
        return existData;
    }
}
