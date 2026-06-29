using System;
using System.Linq;
using Backend.Application.DTOs.StockTakes;
using Backend.Domain.Entities;
using Backend.Share.Helpers;

namespace Backend.Application.Mappings;

public static class StockTakeMapping
{
    public static StockTake ToEntity(this CreateStockTakeDto dto)
    {
        var now = DateTimeHelper.VietnamNow();
        var entity = new StockTake
        {
            WarehouseId = dto.WarehouseId,
            StockTakeStatusId = (int)Backend.Domain.Enums.Enums.StockTakeStatusEnum.Draft,
            STCode = string.IsNullOrEmpty(dto.STCode) ? $"ST{now:yyyyMMddHHmmss}" : dto.STCode,
            Note = dto.Note,
            CreatedDate = now,
            CreatedBy = dto.CreatedBy,
            StockTakeItems = dto.StockTakeItems.Select(item => new StockTakeItem
            {
                ProductVariantId = item.ProductVariantId,
                LocationId = item.LocationId,
                SystemQuantity = 0, // Will be fetched from Inventory
                ActualQuantity = item.ActualQuantity,
                Note = item.Note,
                QRScanned = item.QRScanned,
                CreatedDate = now
            }).ToList()
        };
        return entity;
    }

    public static StockTake ToEntity(this UpdateStockTakeDto dto, StockTake existData)
    {
        existData.StockTakeStatusId = dto.StockTakeStatusId;
        existData.Note = dto.Note;
        existData.StartedDate = dto.StartedDate;
        existData.CompletedDate = dto.CompletedDate;
        existData.ApprovedByUserId = dto.ApprovedByUserId;
        existData.UpdatedBy = dto.UpdatedBy;
        existData.LastModifiedDate = DateTimeHelper.VietnamNow();

        // Mapping items handled separately in the service since we need to diff added/updated/deleted

        return existData;
    }

    public static StockTakeDto ToDto(this StockTake entity)
    {
        return new StockTakeDto
        {
            Id = entity.Id,
            WarehouseId = entity.WarehouseId,
            StockTakeStatusId = entity.StockTakeStatusId,
            STCode = entity.STCode,
            Note = entity.Note,
            StartedDate = entity.StartedDate,
            CompletedDate = entity.CompletedDate,
            ApprovedByUserId = entity.ApprovedByUserId,
            ApproveNote = entity.ApproveNote,
            CreatedDate = entity.CreatedDate,
            StockTakeItems = entity.StockTakeItems.Select(item => new StockTakeItemDto
            {
                Id = item.Id,
                StockTakeId = item.StockTakeId,
                ProductVariantId = item.ProductVariantId,
                LocationId = item.LocationId,
                SystemQuantity = item.SystemQuantity,
                ActualQuantity = item.ActualQuantity,
                Difference = item.Difference,
                Note = item.Note,
                QRScanned = item.QRScanned
            }).ToList()
        };
    }
}
