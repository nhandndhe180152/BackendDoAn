using System;
using Backend.Application.DTOs.CustomerReturnOrderStatuses;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class CustomerReturnOrderStatusMapping
{
    public static CustomerReturnOrderStatus ToEntity(this CreateCustomerReturnOrderStatusDto dto)
    {
        return new CustomerReturnOrderStatus
        {
            Code = dto.Code,
            Name = dto.Name,
            Color = dto.Color,
            CreatedBy = dto.CreatedBy,
            CreatedDate = DateTime.Now,
            IsDeleted = false,
        };
    }

    public static CustomerReturnOrderStatus ToEntity(this UpdateCustomerReturnOrderStatusDto dto, CustomerReturnOrderStatus existData)
    {
        existData.Code = dto.Code;
        existData.Name = dto.Name;
        existData.Color = dto.Color;
        existData.UpdatedBy = dto.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;
        return existData;
    }
}
