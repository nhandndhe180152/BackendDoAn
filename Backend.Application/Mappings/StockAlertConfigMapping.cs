using System;
using Backend.Application.DTOs.StockAlertConfigs;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class StockAlertConfigMapping
{
    public static StockAlertConfig ToEntity(this CreateStockAlertConfigDto obj)
    {
        return new StockAlertConfig
        {
            WarehouseId = obj.WarehouseId,
            ProductVariantId = obj.ProductVariantId,
            MinThreshold = obj.MinThreshold,
            IsActive = obj.IsActive,
            CreatedBy = obj.CreatedBy,
            CreatedDate = DateTime.Now
        };
    }

    public static StockAlertConfig ToEntity(this UpdateStockAlertConfigDto obj, StockAlertConfig existData)
    {
        existData.WarehouseId = obj.WarehouseId;
        existData.ProductVariantId = obj.ProductVariantId;
        existData.MinThreshold = obj.MinThreshold;
        existData.IsActive = obj.IsActive;
        existData.UpdatedBy = obj.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;
        return existData;
    }

    public static StockAlertConfigDetailDto ToDto(this StockAlertConfig entity)
    {
        return new StockAlertConfigDetailDto
        {
            Id = entity.Id,
            WarehouseId = entity.WarehouseId,
            WarehouseCode = entity.Warehouse?.Code,
            WarehouseName = entity.Warehouse?.Name,
            ProductVariantId = entity.ProductVariantId,
            ProductVariantSku = entity.ProductVariant?.SKU,
            ProductName = entity.ProductVariant?.Product?.Name,
            MinThreshold = entity.MinThreshold,
            IsActive = entity.IsActive,
            CreatedDate = entity.CreatedDate,
            LastModifiedDate = entity.LastModifiedDate
        };
    }
}
