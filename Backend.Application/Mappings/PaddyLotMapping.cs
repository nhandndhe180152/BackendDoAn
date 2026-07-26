using System;
using Backend.Application.DTOs.PaddyLots;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class PaddyLotMapping
{
    public static PaddyLot ToEntity(this CreatePaddyLotDto dto, string lotCode)
    {
        return new PaddyLot
        {
            OrganizationId = dto.OrganizationId,
            LotCode = lotCode,
            LotType = dto.LotType.Trim().ToUpper(),
            ProductVariantId = dto.ProductVariantId,
            RiceVarietyId = dto.RiceVarietyId,
            StatusId = dto.StatusId,
            SourceReceiptId = dto.SourceReceiptId,
            SourceMillingOrderId = dto.SourceMillingOrderId,
            WarehouseId = dto.WarehouseId,
            LocationId = dto.LocationId,
            InboundDate = dto.InboundDate,
            InitialWeightKg = dto.InitialWeightKg,
            RemainingWeightKg = dto.InitialWeightKg,
            CostPricePerKg = dto.CostPricePerKg,
            QualityStatus = dto.QualityStatus?.Trim(),
            CreatedBy = dto.CreatedBy,
            CreatedDate = DateTime.Now
        };
    }

    public static PaddyLot ToEntity(this UpdatePaddyLotDto dto, PaddyLot existData)
    {
        existData.OrganizationId = dto.OrganizationId;
        existData.LotType = dto.LotType.Trim().ToUpper();
        existData.ProductVariantId = dto.ProductVariantId;
        existData.RiceVarietyId = dto.RiceVarietyId;
        existData.StatusId = dto.StatusId;
        existData.SourceReceiptId = dto.SourceReceiptId;
        existData.SourceMillingOrderId = dto.SourceMillingOrderId;
        existData.WarehouseId = dto.WarehouseId;
        existData.LocationId = dto.LocationId;
        existData.InboundDate = dto.InboundDate;
        existData.InitialWeightKg = dto.InitialWeightKg;
        existData.RemainingWeightKg = dto.RemainingWeightKg;
        existData.CostPricePerKg = dto.CostPricePerKg;
        existData.QualityStatus = dto.QualityStatus?.Trim();
        existData.UpdatedBy = dto.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;
        return existData;
    }

    public static PaddyLotDetailDto ToDto(this PaddyLot entity)
    {
        return new PaddyLotDetailDto
        {
            Id = entity.Id,
            OrganizationId = entity.OrganizationId,
            LotCode = entity.LotCode,
            LotType = entity.LotType,
            ProductVariantId = entity.ProductVariantId,
            SKU = entity.ProductVariant?.SKU,
            ProductVariantName = entity.ProductVariant?.Name,
            RiceVarietyId = entity.RiceVarietyId,
            RiceVarietyName = entity.RiceVariety?.Name,
            StatusId = entity.StatusId,
            StatusName = entity.Status?.Name,
            SourceReceiptId = entity.SourceReceiptId,
            SourceMillingOrderId = entity.SourceMillingOrderId,
            WarehouseId = entity.WarehouseId,
            WarehouseName = entity.Warehouse?.Name,
            LocationId = entity.LocationId,
            LocationCode = entity.Location != null ? (entity.Location.SlotCode ?? entity.Location.QrCode) : null,
            InboundDate = entity.InboundDate,
            InitialWeightKg = entity.InitialWeightKg,
            RemainingWeightKg = entity.RemainingWeightKg,
            CostPricePerKg = entity.CostPricePerKg,
            QualityStatus = entity.QualityStatus,
            CreatedDate = entity.CreatedDate,
            LastModifiedDate = entity.LastModifiedDate
        };
    }
}
