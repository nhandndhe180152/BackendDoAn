using System;
using Backend.Application.DTOs.PurchaseOrderStatuses;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class PurchaseOrderStatusMapping
{
    public static PurchaseOrderStatus ToEntity(this CreatePurchaseOrderStatusDto dto)
    {
        return new PurchaseOrderStatus
        {
            Code = dto.Code.Trim().ToUpperInvariant(),
            Name = dto.Name.Trim(),
            Color = dto.Color.Trim(),
            CreatedBy = dto.CreatedBy,
            CreatedDate = DateTime.Now,
            IsDeleted = false,
        };
    }

    public static PurchaseOrderStatus ToEntity(this UpdatePurchaseOrderStatusDto dto, PurchaseOrderStatus existData)
    {
        existData.Name = dto.Name.Trim();
        existData.Color = dto.Color.Trim();
        existData.UpdatedBy = dto.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;
        return existData;
    }
}
