using System;
using Backend.Application.DTOs.ProductAttributes;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class ProductAttributeMapping
{
    public static ProductAttribute ToEntity(this CreateProductAttributeDto obj)
    {
        return new ProductAttribute
        {
            Name = obj.Name,
            Description = obj.Description,
            CreatedBy = obj.CreatedBy,
            CreatedDate = DateTime.Now
        };
    }

    public static ProductAttribute ToEntity(this UpdateProductAttributeDto obj, ProductAttribute existData)
    {
        existData.Name = obj.Name;
        existData.Description = obj.Description;
        existData.UpdatedBy = obj.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;
        return existData;
    }

    public static ProductAttributeDetailDto ToDto(this ProductAttribute entity)
    {
        return new ProductAttributeDetailDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            CreatedDate = entity.CreatedDate
        };
    }
}
