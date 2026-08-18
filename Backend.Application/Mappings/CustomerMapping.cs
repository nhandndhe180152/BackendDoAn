using System;
using Backend.Application.DTOs.Customers;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class CustomerMapping
{
    public static Customer ToEntity(this CreateCustomerDto obj)
    {
        return new Customer
        {
            OrganizationId = obj.OrganizationId,
            Code = obj.Code.Trim(),
            Name = obj.Name.Trim(),
            CustomerType = obj.CustomerType?.Trim(),
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

    public static Customer ToEntity(this UpdateCustomerDto obj, Customer existData)
    {
        existData.OrganizationId = obj.OrganizationId;
        existData.Code = obj.Code.Trim();
        existData.Name = obj.Name.Trim();
        existData.CustomerType = obj.CustomerType?.Trim();
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

    public static CustomerDetailDto ToDto(this Customer entity)
    {
        return new CustomerDetailDto
        {
            Id = entity.Id,
            OrganizationId = entity.OrganizationId,
            Code = entity.Code,
            Name = entity.Name,
            CustomerType = entity.CustomerType,
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
