using System;
using Backend.Application.DTOs.Suppliers;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class SupplierMapping
{
    public static Supplier ToEntity(this CreateSupplierDto obj)
    {
        return new Supplier
        {
            Name = obj.Name.Trim(),
            Code = obj.Code.Trim(),
            ContactPerson = obj.ContactPerson?.Trim(),
            Phone = obj.Phone?.Trim(),
            Email = obj.Email?.Trim(),
            Address = obj.Address?.Trim(),
            TaxCode = obj.TaxCode?.Trim(),
            IsActive = obj.IsActive,
            CreatedBy = obj.CreatedBy,
            CreatedDate = DateTime.Now
        };
    }

    public static Supplier ToEntity(this UpdateSupplierDto obj, Supplier existData)
    {
        existData.Name = obj.Name.Trim();
        existData.Code = obj.Code.Trim();
        existData.ContactPerson = obj.ContactPerson?.Trim();
        existData.Phone = obj.Phone?.Trim();
        existData.Email = obj.Email?.Trim();
        existData.Address = obj.Address?.Trim();
        existData.TaxCode = obj.TaxCode?.Trim();
        existData.IsActive = obj.IsActive;
        existData.UpdatedBy = obj.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;
        return existData;
    }

    public static SupplierDetailDto ToDto(this Supplier entity)
    {
        return new SupplierDetailDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Code = entity.Code,
            ContactPerson = entity.ContactPerson,
            Phone = entity.Phone,
            Email = entity.Email,
            Address = entity.Address,
            TaxCode = entity.TaxCode,
            IsActive = entity.IsActive,
            CreatedDate = entity.CreatedDate,
            LastModifiedDate = entity.LastModifiedDate
        };
    }
}
