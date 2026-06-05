using System;
using Backend.Application.DTOs.Products;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class ProductMapping
{
    public static Product ToEntity(this CreateProductDto obj)
    {
        return new Product
        {
            Name = obj.Name,
            Description = obj.Description,
            ProductCategoryId = obj.ProductCategoryId,
            IsActive = obj.IsActive,
            CreatedBy = obj.CreatedBy,
            CreatedDate = DateTime.Now
        };
    }

    public static Product ToEntity(this UpdateProductDto obj, Product existData)
    {
        existData.Name = obj.Name;
        existData.Description = obj.Description;
        existData.ProductCategoryId = obj.ProductCategoryId;
        existData.IsActive = obj.IsActive;
        existData.UpdatedBy = obj.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;
        return existData;
    }

    public static ProductDetailDto ToDto(this Product entity)
    {
        return new ProductDetailDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            ProductCategoryId = entity.ProductCategoryId,
            ProductCategoryName = entity.ProductCategory?.Name,
            IsActive = entity.IsActive,
            CreatedDate = entity.CreatedDate
        };
    }
}
