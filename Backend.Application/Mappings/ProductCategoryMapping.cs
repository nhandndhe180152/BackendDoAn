using System;
using System.Linq;
using Backend.Application.DTOs.ProductCategories;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class ProductCategoryMapping
{
    public static ProductCategory ToEntity(this CreateProductCategoryDto obj)
    {
        return new ProductCategory
        {
            Name = obj.Name,
            Description = obj.Description,
            ParentId = obj.ParentId,
            TreeIds = obj.TreeIds,
            SortOrder = obj.SortOrder,
            CreatedBy = obj.CreatedBy,
            CreatedDate = DateTime.Now
        };
    }

    public static ProductCategory ToEntity(this UpdateProductCategoryDto obj, ProductCategory existData)
    {
        existData.Name = obj.Name;
        existData.Description = obj.Description;
        existData.ParentId = obj.ParentId;
        existData.TreeIds = obj.TreeIds;
        existData.SortOrder = obj.SortOrder;
        existData.UpdatedBy = obj.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;
        return existData;
    }

    public static ProductCategoryDetailDto ToDto(this ProductCategory entity)
    {
        return new ProductCategoryDetailDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            ParentId = entity.ParentId,
            ParentName = entity.ParentCategory?.Name,
            TreeIds = entity.TreeIds,
            SortOrder = entity.SortOrder,
            CreatedDate = entity.CreatedDate,
            ProductCount = entity.Products != null ? entity.Products.Count(p => !p.IsDeleted) : 0
        };
    }
}
