using System;
using System.Linq;
using System.Text.Json;
using Backend.Application.DTOs.PaddyPurchaseReceipts;
using Backend.Application.DTOs.PaddyPurchaseSchedules;
using Backend.Domain.Entities;
using Backend.Share.Helpers;

namespace Backend.Application.Mappings;

public static class PaddyPurchaseMapping
{
    // ── Schedule ────────────────────────────────────────────────────────────

    public static PaddyPurchaseSchedule ToEntity(this CreatePaddyPurchaseScheduleDto dto, string scheduleCode)
    {
        return new PaddyPurchaseSchedule
        {
            OrganizationId = dto.OrganizationId,
            ScheduleCode = scheduleCode,
            FarmerId = dto.FarmerId,
            StatusId = dto.StatusId,
            RiceVarietyId = dto.RiceVarietyId,
            ScheduleDate = dto.ScheduleDate,
            Location = dto.Location?.Trim(),
            EstimatedQtyKg = dto.EstimatedQtyKg,
            ExpectedPrice = dto.ExpectedPrice,
            AssignedUserId = dto.AssignedUserId,
            WarehouseId = dto.WarehouseId,
            Note = dto.Note?.Trim(),
            CreatedBy = dto.CreatedBy,
            CreatedDate = DateTime.Now
        };
    }

    public static PaddyPurchaseSchedule ToEntity(this UpdatePaddyPurchaseScheduleDto dto, PaddyPurchaseSchedule existData)
    {
        existData.OrganizationId = dto.OrganizationId;
        existData.FarmerId = dto.FarmerId;
        existData.StatusId = dto.StatusId;
        existData.RiceVarietyId = dto.RiceVarietyId;
        existData.ScheduleDate = dto.ScheduleDate;
        existData.Location = dto.Location?.Trim();
        existData.EstimatedQtyKg = dto.EstimatedQtyKg;
        existData.ExpectedPrice = dto.ExpectedPrice;
        existData.AssignedUserId = dto.AssignedUserId;
        existData.WarehouseId = dto.WarehouseId;
        existData.Note = dto.Note?.Trim();
        existData.UpdatedBy = dto.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;
        return existData;
    }

    /// <summary>
    /// Map lịch thu mua sang DTO chi tiết.
    /// <paramref name="receiptCount"/>/<paramref name="receiptedWeightKg"/> là thống kê phiếu mua
    /// chưa xóa thuộc lịch — dùng để tính cờ CanCreateReceipt cho web &amp; mobile.
    /// </summary>
    public static PaddyPurchaseScheduleDetailDto ToDto(
        this PaddyPurchaseSchedule entity,
        int receiptCount = 0,
        decimal receiptedWeightKg = 0m)
    {
        var statusCode = entity.Status?.Code;
        return new PaddyPurchaseScheduleDetailDto
        {
            ReceiptCount = receiptCount,
            ReceiptedWeightKg = receiptedWeightKg,
            RemainingQtyKg = PaddyScheduleReceiptRule.RemainingQtyKg(entity.EstimatedQtyKg, receiptedWeightKg),
            CanCreateReceipt = PaddyScheduleReceiptRule.CanCreateReceipt(
                statusCode, entity.EstimatedQtyKg, receiptedWeightKg, receiptCount),
            Id = entity.Id,
            OrganizationId = entity.OrganizationId,
            ScheduleCode = entity.ScheduleCode,
            FarmerId = entity.FarmerId,
            FarmerName = entity.Farmer?.Name,
            StatusId = entity.StatusId,
            StatusName = entity.Status?.Name,
            StatusCode = statusCode,
            RiceVarietyId = entity.RiceVarietyId,
            RiceVarietyName = entity.RiceVariety?.Name,
            ScheduleDate = entity.ScheduleDate,
            Location = entity.Location,
            EstimatedQtyKg = entity.EstimatedQtyKg,
            ExpectedPrice = entity.ExpectedPrice,
            AssignedUserId = entity.AssignedUserId,
            WarehouseId = entity.WarehouseId,
            WarehouseName = entity.Warehouse?.Name,
            Note = entity.Note,
            CreatedDate = entity.CreatedDate,
            LastModifiedDate = entity.LastModifiedDate
        };
    }

    // ── Receipt ─────────────────────────────────────────────────────────────

    public static PaddyPurchaseReceipt ToEntity(this CreatePaddyPurchaseReceiptDto dto, string receiptCode)
    {
        return new PaddyPurchaseReceipt
        {
            OrganizationId = dto.OrganizationId,
            ReceiptCode = receiptCode,
            ScheduleId = dto.ScheduleId,
            FarmerId = dto.FarmerId,
            RiceVarietyId = dto.RiceVarietyId,
            ProductVariantId = dto.ProductVariantId,
            WarehouseId = dto.WarehouseId,
            ActualWeightKg = dto.Bags?.Sum(x => x.WeightKg) ?? dto.ActualWeightKg,
            BagCount = dto.Bags?.Count ?? dto.BagCount,
            BagDetailsJson = dto.Bags is { Count: > 0 } ? JsonSerializer.Serialize(dto.Bags) : null,
            AgreedPrice = dto.AgreedPrice,
            TotalAmount = dto.TotalAmount,
            PaidAmount = dto.PaidAmount,
            DebtAmount = dto.DebtAmount,
            QualityJson = dto.QualityJson,
            PriceAdjustReason = dto.PriceAdjustReason?.Trim(),
            ReceiptDate = dto.ReceiptDate,
            CreatedBy = dto.CreatedBy,
            CreatedDate = DateTime.Now
        };
    }

    public static PaddyPurchaseReceipt ToEntity(this UpdatePaddyPurchaseReceiptDto dto, PaddyPurchaseReceipt existData)
    {
        existData.OrganizationId = dto.OrganizationId;
        existData.ScheduleId = dto.ScheduleId;
        existData.FarmerId = dto.FarmerId;
        existData.RiceVarietyId = dto.RiceVarietyId;
        existData.ProductVariantId = dto.ProductVariantId;
        existData.WarehouseId = dto.WarehouseId;
        existData.ActualWeightKg = dto.Bags?.Sum(x => x.WeightKg) ?? dto.ActualWeightKg;
        existData.BagCount = dto.Bags?.Count ?? dto.BagCount;
        existData.BagDetailsJson = dto.Bags is { Count: > 0 } ? JsonSerializer.Serialize(dto.Bags) : existData.BagDetailsJson;
        existData.AgreedPrice = dto.AgreedPrice;
        existData.TotalAmount = dto.TotalAmount;
        existData.PaidAmount = dto.PaidAmount;
        existData.DebtAmount = dto.DebtAmount;
        existData.QualityJson = dto.QualityJson;
        existData.PriceAdjustReason = dto.PriceAdjustReason?.Trim();
        existData.ReceiptDate = dto.ReceiptDate;
        existData.UpdatedBy = dto.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;
        return existData;
    }

    public static PaddyPurchaseReceiptDetailDto ToDto(this PaddyPurchaseReceipt entity)
    {
        return new PaddyPurchaseReceiptDetailDto
        {
            Id = entity.Id,
            OrganizationId = entity.OrganizationId,
            ReceiptCode = entity.ReceiptCode,
            ScheduleId = entity.ScheduleId,
            ScheduleCode = entity.Schedule?.ScheduleCode,
            FarmerId = entity.FarmerId,
            FarmerName = entity.Farmer?.Name,
            RiceVarietyId = entity.RiceVarietyId,
            RiceVarietyName = entity.RiceVariety?.Name,
            ProductVariantId = entity.ProductVariantId,
            ProductVariantName = entity.ProductVariant?.Name,
            ProductVariantSku = entity.ProductVariant?.SKU,
            WarehouseId = entity.WarehouseId,
            WarehouseName = entity.Warehouse?.Name,
            ActualWeightKg = entity.ActualWeightKg,
            AcceptedWeightKg = entity.AcceptedWeightKg,
            RejectedWeightKg = entity.RejectedWeightKg,
            BagCount = entity.BagCount,
            Bags = string.IsNullOrWhiteSpace(entity.BagDetailsJson)
                ? new()
                : JsonSerializer.Deserialize<System.Collections.Generic.List<DTOs.InboundOrders.CreateBagDto>>(entity.BagDetailsJson) ?? new(),
            AgreedPrice = entity.AgreedPrice,
            TotalAmount = entity.TotalAmount,
            PaidAmount = entity.PaidAmount,
            DebtAmount = entity.DebtAmount,
            RefundReceivableAmount = Math.Max(0m, entity.PaidAmount - entity.TotalAmount),
            DebtDueDate = entity.DebtDueDate,
            QcFinalizedAt = entity.QcFinalizedAt,
            QcFinalizedBy = entity.QcFinalizedBy,
            QualityJson = entity.QualityJson,
            PriceAdjustReason = entity.PriceAdjustReason,
            ReceiptDate = entity.ReceiptDate,
            PaddyLotId = entity.PaddyLot?.Id,
            IsConfirmed = entity.PaddyLot != null,
            CreatedDate = entity.CreatedDate,
            LastModifiedDate = entity.LastModifiedDate
        };
    }
}
