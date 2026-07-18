using System;
using System.Collections.Generic;
using System.Text.Json;
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
            SKU = obj.SKU?.Trim().ToUpperInvariant() ?? string.Empty,
            QRCode = obj.QRCode,
            CostPrice = obj.CostPrice,
            SalePrice = obj.SalePrice,
            Weight = obj.Weight,
            AttributeValues = obj.AttributeValues,
            ImageId = obj.ImageId,
            IsActive = obj.IsActive,
            MinStockLevel = obj.MinStockLevel,
            RiceVarietyId = obj.RiceVarietyId, // Map thêm giống lúa
            CreatedBy = obj.CreatedBy,
            CreatedDate = DateTime.Now
        };
    }
 
    public static ProductVariant ToEntity(this UpdateProductVariantDto obj, ProductVariant existData)
    {
        existData.Name = obj.Name;
        existData.Description = obj.Description;
        existData.UnitOfMeasureId = obj.UnitOfMeasureId;
        existData.CostPrice = obj.CostPrice;
        existData.SalePrice = obj.SalePrice;
        existData.Weight = obj.Weight;
        existData.AttributeValues = obj.AttributeValues;
        existData.ImageId = obj.ImageId;
        existData.IsActive = obj.IsActive;
        existData.MinStockLevel = obj.MinStockLevel;
        existData.RiceVarietyId = obj.RiceVarietyId; // Map thêm giống lúa khi update
        existData.UpdatedBy = obj.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;
        return existData;
    }

    /// <param name="imageUrl">Pre-resolved image URL from storage service</param>
    /// <param name="categoryIsDeleted">Whether the parent ProductCategory is soft-deleted</param>
    /// <param name="attributeLookup">Optional map of attributeId → name for JSON parsing</param>
    public static ProductVariantDetailDto ToDto(
        this ProductVariant entity,
        string? imageUrl = null,
        bool categoryIsDeleted = false,
        Dictionary<int, string>? attributeLookup = null)
    {
        // Parse AttributeValues: try JSON first, fall back to legacy text
        List<AttributeValueDto>? parsedAttributes = null;
        string? legacyText = null;

        if (!string.IsNullOrWhiteSpace(entity.AttributeValues))
        {
            try
            {
                var raw = JsonSerializer.Deserialize<List<RawAttributeEntry>>(entity.AttributeValues,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (raw != null)
                {
                    parsedAttributes = raw.Select(x => new AttributeValueDto
                    {
                        AttributeId = x.AttributeId,
                        AttributeName = attributeLookup != null && attributeLookup.TryGetValue(x.AttributeId, out var n) ? n : null,
                        Value = x.Value ?? string.Empty
                    }).ToList();
                }
            }
            catch (JsonException)
            {
                legacyText = entity.AttributeValues;
            }
        }

        bool productActive = entity.Product?.IsActive ?? false;
        bool productDeleted = entity.Product?.IsDeleted ?? false;

        return new ProductVariantDetailDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            ProductId = entity.ProductId,
            ProductName = entity.Product?.Name,
            ProductIsActive = productActive,
            ProductCategoryId = entity.Product?.ProductCategoryId,
            ProductCategoryName = entity.Product?.ProductCategory?.Name,
            UnitOfMeasureId = entity.UnitOfMeasureId,
            UnitOfMeasureName = entity.UnitOfMeasure?.Name,
            SKU = entity.SKU,
            QRCode = entity.QRCode,
            CostPrice = entity.CostPrice,
            SalePrice = entity.SalePrice,
            Weight = entity.Weight,
            ImageId = entity.ImageId,
            ImageUrl = imageUrl,
            IsActive = entity.IsActive,
            IsDeleted = entity.IsDeleted,
            MinStockLevel = entity.MinStockLevel,
            RiceVarietyId = entity.RiceVarietyId, // Trả về RiceVarietyId
            AttributeValuesJson = parsedAttributes,
            LegacyAttributeValues = legacyText,
            EffectiveActiveStatus = entity.IsActive && !entity.IsDeleted && productActive && !productDeleted && !categoryIsDeleted,
            CreatedDate = entity.CreatedDate,
            LastModifiedDate = entity.LastModifiedDate
        };
    }

    /// Internal model for deserializing raw AttributeValues JSON
    private class RawAttributeEntry
    {
        public int AttributeId { get; set; }
        public string? Value { get; set; }
    }
}

