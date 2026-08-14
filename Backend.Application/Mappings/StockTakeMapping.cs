using System;
using System.Collections.Generic;
using System.Linq;
using Backend.Application.Common;
using Backend.Application.Constants;
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
            StockTakeStatusId = Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Draft),
            STCode = string.IsNullOrEmpty(dto.STCode) ? $"ST{now:yyyyMMddHHmmss}" : dto.STCode,
            Note = dto.Note,
            CreatedDate = now,
            CreatedBy = dto.CreatedBy,
            StockTakeItems = dto.StockTakeItems.Select(item => new StockTakeItem
            {
                ProductVariantId  = item.ProductVariantId,
                LocationId        = item.LocationId,
                PaddyLotId        = item.PaddyLotId,
                SystemQuantity    = 0, // Will be fetched from Inventory in service
                ActualQuantity    = item.ActualQuantity,
                Note              = item.Note,
                QRScanned         = item.QRScanned,
                RecountConfirmed  = false, // STK-02: backend chưa xác nhận — không cho client tự set true khi tạo mới
                CreatedDate       = now
            }).ToList()
        };
        return entity;
    }

    public static StockTake ToEntity(this UpdateStockTakeDto dto, StockTake existData)
    {
        existData.StockTakeStatusId   = dto.StockTakeStatusId;
        existData.Note                = dto.Note;
        existData.StartedDate         = dto.StartedDate;
        existData.CompletedDate       = dto.CompletedDate;
        existData.ApprovedByUserId    = dto.ApprovedByUserId;
        existData.UpdatedBy           = dto.UpdatedBy;
        existData.LastModifiedDate    = DateTimeHelper.VietnamNow();

        // Mapping items handled separately in the service since we need to diff added/updated/deleted

        return existData;
    }

    public static StockTakeDto ToDto(this StockTake entity)
    {
        return new StockTakeDto
        {
            Id                  = entity.Id,
            WarehouseId         = entity.WarehouseId,
            StockTakeStatusId   = entity.StockTakeStatusId,
            STCode              = entity.STCode,
            Note                = entity.Note,
            StartedDate         = entity.StartedDate,
            CompletedDate       = entity.CompletedDate,
            ApprovedByUserId    = entity.ApprovedByUserId,
            ApproveNote         = entity.ApproveNote,
            CreatedDate         = entity.CreatedDate,
            LastModifiedDate    = entity.LastModifiedDate,
            WarehouseName       = entity.Warehouse?.Name,
            StockTakeStatusCode = entity.StockTakeStatus?.Code,
            StockTakeStatusName = entity.StockTakeStatus?.Name,
            StockTakeStatusColor = entity.StockTakeStatus?.Color,
            CreatedByUserId     = entity.CreatedBy,
            StockTakeItems      = entity.StockTakeItems.Select(item => new StockTakeItemDto
            {
                Id                   = item.Id,
                StockTakeId          = item.StockTakeId,
                ProductVariantId     = item.ProductVariantId,
                LocationId           = item.LocationId,
                PaddyLotId           = item.PaddyLotId,
                SKU                  = item.ProductVariant?.SKU,
                ProductVariantName   = item.ProductVariant?.Name,
                UnitWeightKg         = item.ProductVariant?.Weight ?? 0m,
                LocationCode         = item.Location?.SlotCode,
                ZoneName             = item.Location?.ZoneName,
                LotCode              = item.PaddyLot?.LotCode,
                IsQuarantine         = item.Location?.IsQuarantine ?? false,
                SystemQuantity       = item.SystemQuantity,
                ActualQuantity       = item.ActualQuantity,
                Difference           = item.Difference,
                AbsoluteVarianceKg   = item.ActualQuantity.HasValue ? item.AbsoluteVarianceKg : null,
                // VariancePercent và VarianceSeverity được enriched trong StockTakeService
                VariancePercent      = item.VariancePercent,
                VarianceSeverity     = item.VarianceSeverity,
                Note                 = item.Note,
                QRScanned            = item.QRScanned,
                RecountConfirmed     = item.RecountConfirmed,
                RecountConfirmedBy   = item.RecountConfirmedBy,
                RecountConfirmedAt   = item.RecountConfirmedAt,
                SystemBagCount       = item.SystemBagCount,
                CountedBagCount      = item.CountedBagCount,
                BagDifference        = item.BagDifference,
                WeighedBagCount      = item.Bags?.Count(b => !b.IsDeleted && b.CountedWeightKg.HasValue) ?? 0,
                QualityStatus        = item.QualityStatus,
                QualityNote          = item.QualityNote,
                QualityImageUrls     = item.QualityImageUrls,
                IsQualityFailed      = item.IsQualityFailed,
                Bags                 = item.Bags == null
                    ? new List<StockTakeItemBagDto>()
                    : item.Bags
                        .Where(b => !b.IsDeleted)
                        .OrderBy(b => b.BagNo)
                        .Select(b => b.ToDto())
                        .ToList()
            }).ToList()
        };
    }

    public static StockTakeItemBagDto ToDto(this StockTakeItemBag bag) => new()
    {
        Id               = bag.Id,
        StockTakeItemId  = bag.StockTakeItemId,
        PaddyLotBagId    = bag.PaddyLotBagId,
        BagNo            = bag.BagNo,
        QrCode           = bag.QrCode,
        SystemWeightKg   = bag.SystemWeightKg,
        CountedWeightKg  = bag.CountedWeightKg,
        Counted          = bag.Counted,
        ScannedByQr      = bag.ScannedByQr,
        IsUnexpected     = bag.IsUnexpected,
        IsMissing        = bag.IsMissing,
        WeightDifference = bag.WeightDifference,
        QualityStatus    = bag.QualityStatus,
        Note             = bag.Note,
        CountedAt        = bag.CountedAt,
        CountedByUserId  = bag.CountedByUserId
    };
}
