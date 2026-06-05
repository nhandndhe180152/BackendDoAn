using System;
using Backend.Application.DTOs.ProductVariants;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class ProductVariantMapping
{
    public static ProductVariant ToEntity(this CreateProductVariantDto obj)
    {
        return new ProductVariant
        {
            Name = obj.Name,
            Description = obj.Description,
            ProductId = obj.ProductId,
            UnitOfMeasureId = obj.UnitOfMeasureId,
            SKU = obj.SKU,
            QRCode = obj.QRCode,
            CostPrice = obj.CostPrice,
            SalePrice = obj.SalePrice,
            Weight = obj.Weight,
            AttributeValues = obj.AttributeValues,
            ImageId = obj.ImageId,
            IsActive = obj.IsActive,
            MinStockLevel = obj.MinStockLevel,
            CreatedBy = obj.CreatedBy,
            CreatedDate = DateTime.Now
        };
    }

    public static ProductVariant ToEntity(this UpdateProductVariantDto obj, ProductVariant existData)
    {
        existData.Name = obj.Name;
        existData.Description = obj.Description;
        existData.ProductId = obj.ProductId;
        existData.UnitOfMeasureId = obj.UnitOfMeasureId;
        existData.SKU = obj.SKU;
        existData.QRCode = obj.QRCode;
        existData.CostPrice = obj.CostPrice;
        existData.SalePrice = obj.SalePrice;
        existData.Weight = obj.Weight;
        existData.AttributeValues = obj.AttributeValues;
        existData.ImageId = obj.ImageId;
        existData.IsActive = obj.IsActive;
        existData.MinStockLevel = obj.MinStockLevel;
        existData.UpdatedBy = obj.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;
        return existData;
    }

    public static ProductVariantDetailDto ToDto(this ProductVariant entity, string? imageUrl = null)
    {
        return new ProductVariantDetailDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            ProductId = entity.ProductId,
            ProductName = entity.Product?.Name,
            UnitOfMeasureId = entity.UnitOfMeasureId,
            UnitOfMeasureName = entity.UnitOfMeasure?.Name,
            SKU = entity.SKU,
            QRCode = entity.QRCode,
            CostPrice = entity.CostPrice,
            SalePrice = entity.SalePrice,
            Weight = entity.Weight,
            AttributeValues = entity.AttributeValues,
            ImageId = entity.ImageId,
            ImageUrl = imageUrl,
            IsActive = entity.IsActive,
            MinStockLevel = entity.MinStockLevel,
            CreatedDate = entity.CreatedDate
        };
    }
}
