using System;
using System.Linq;
using Backend.Application.DTOs.InboundOrders;
using Backend.Domain.Entities;
using Newtonsoft.Json;

namespace Backend.Application.Mappings;

public static class InboundOrderMapping
{
    public static InboundOrder ToEntity(this CreateInboundOrderDto dto)
    {
        var entity = new InboundOrder
        {
            WarehouseId = dto.WarehouseId,
            SupplierId = dto.SupplierId,
            ExpectedDate = dto.ExpectedDate,
            Note = dto.Note,
            TotalAssetValue = 0, // Calculated dynamically
            CreatedBy = dto.CreatedBy,
            CreatedDate = DateTime.Now
        };

        if (dto.Items != null)
        {
            entity.InboundOrderItems = dto.Items.Select(item => new InboundOrderItem
            {
                ProductVariantId = item.ProductVariantId,
                QuantityOrdered = item.QuantityOrdered,
                QuantityReceived = 0,
                UnitCostPrice = item.UnitCostPrice,
                ExpectedWeightKg = 0,
                ActualWeightKg = 0,
                QRScanned = false,
                Note = item.Note, // Simple note initially, no receipt state JSON yet
                CreatedBy = dto.CreatedBy,
                CreatedDate = DateTime.Now
            }).ToList();
        }

        return entity;
    }

    public static InboundOrderListDto ToListDto(this InboundOrder entity)
    {
        return new InboundOrderListDto
        {
            Id = entity.Id,
            POCode = entity.POCode,
            WarehouseId = entity.WarehouseId,
            WarehouseName = entity.Warehouse?.Name ?? string.Empty,
            SupplierId = entity.SupplierId,
            SupplierName = entity.SourceType == "RECEIPT" ? entity.PaddyPurchaseReceipt?.Farmer?.Name : entity.Supplier?.Name,
            InboundOrderStatusId = entity.InboundOrderStatusId,
            InboundOrderStatusName = entity.InboundOrderStatus?.Name ?? string.Empty,
            TotalAssetValue = entity.TotalAssetValue,
            ExpectedDate = entity.ExpectedDate,
            CompletedDate = entity.CompletedDate,
            Note = entity.Note,
            CreatedDate = entity.CreatedDate
        };
    }

    public static InboundOrderDetailDto ToDetailDto(this InboundOrder entity)
    {
        return new InboundOrderDetailDto
        {
            Id = entity.Id,
            POCode = entity.POCode,
            WarehouseId = entity.WarehouseId,
            WarehouseName = entity.Warehouse?.Name ?? string.Empty,
            SupplierId = entity.SupplierId,
            SupplierName = entity.SourceType == "RECEIPT" ? entity.PaddyPurchaseReceipt?.Farmer?.Name : entity.Supplier?.Name,
            InboundOrderStatusId = entity.InboundOrderStatusId,
            InboundOrderStatusName = entity.InboundOrderStatus?.Name ?? string.Empty,
            TotalAssetValue = entity.TotalAssetValue,
            ExpectedDate = entity.ExpectedDate,
            CompletedDate = entity.CompletedDate,
            Note = entity.Note,
            CreatedDate = entity.CreatedDate,
            Items = entity.InboundOrderItems?.Select(x => x.ToDto()).ToList() ?? new()
        };
    }

    public static InboundOrderItemDto ToDto(this InboundOrderItem entity)
    {
        var dto = new InboundOrderItemDto
        {
            Id = entity.Id,
            InboundOrderId = entity.InboundOrderId,
            ProductVariantId = entity.ProductVariantId,
            ProductVariantName = entity.ProductVariant?.Name,
            SKU = entity.ProductVariant?.SKU,
            QuantityOrdered = entity.QuantityOrdered,
            QuantityReceived = entity.QuantityReceived,
            UnitCostPrice = entity.UnitCostPrice,
            ExpectedWeightKg = entity.ExpectedWeightKg,
            ActualWeightKg = entity.ActualWeightKg,
            QRScanned = entity.QRScanned,
            Note = entity.Note
        };

        // Try to parse Note as InboundReceiptState JSON
        if (!string.IsNullOrWhiteSpace(entity.Note) && entity.Note.Trim().StartsWith("{") && entity.Note.Trim().EndsWith("}"))
        {
            try
            {
                var state = JsonConvert.DeserializeObject<InboundReceiptState>(entity.Note);
                if (state != null)
                {
                    dto.ReceiptStatus = state.ReceiptStatus;
                    dto.OverReceiveReason = state.OverReceiveReason;
                    dto.WeightDiscrepancyReason = state.WeightDiscrepancyReason;
                    dto.ExceptionDecision = state.ExceptionDecision;
                    dto.ExceptionReason = state.ExceptionReason;
                    dto.ConfirmedLocationId = state.ConfirmedLocationId;
                    dto.ConfirmedLocationCode = state.ConfirmedLocationCode;
                    dto.PutawayOverrideReason = state.PutawayOverrideReason;
                    dto.QuantityEntered = state.QuantityEntered;
                    dto.Note = state.OriginalNote; // Expose original note text to UI
                }
            }
            catch
            {
                // If parsing fails, treat it as a plain string note
            }
        }

        return dto;
    }

    /// <summary>Map DeliveryNote sang DTO. imageUrl được service phân giải từ IStorageService.</summary>
    public static DeliveryNoteDto ToDto(this DeliveryNote entity, int inboundOrderId, string? imageUrl)
    {
        return new DeliveryNoteDto
        {
            Id = entity.Id,
            InboundOrderId = inboundOrderId,
            TrackingCode = entity.TrackingCode,
            CarrierName = entity.CarrierName,
            SenderName = entity.SenderName,
            SenderPhone = entity.SenderPhone,
            SenderAddress = entity.SenderAddress,
            ReceiverName = entity.ReceiverName,
            ReceiverPhone = entity.ReceiverPhone,
            ReceiverAddress = entity.ReceiverAddress,
            DeclaredWeight = entity.DeclaredWeight,
            CODAmount = entity.CODAmount,
            RawOcrText = entity.RawOcrText,
            OriginalImageFileId = entity.OriginalImageFileId,
            ImageUrl = imageUrl,
            IsConfirmed = entity.IsConfirmed,
            CreatedDate = entity.CreatedDate
        };
    }

    public static string SerializeReceiptState(InboundReceiptState state)
    {
        return JsonConvert.SerializeObject(state);
    }

    public static InboundReceiptState GetReceiptState(this InboundOrderItem entity)
    {
        if (!string.IsNullOrWhiteSpace(entity.Note) && entity.Note.Trim().StartsWith("{") && entity.Note.Trim().EndsWith("}"))
        {
            try
            {
                var state = JsonConvert.DeserializeObject<InboundReceiptState>(entity.Note);
                if (state != null)
                {
                    return state;
                }
            }
            catch
            {
                // Fallback to empty state
            }
        }

        return new InboundReceiptState
        {
            ReceiptStatus = "Draft",
            OriginalNote = entity.Note
        };
    }

    public static void SaveReceiptState(this InboundOrderItem entity, InboundReceiptState state)
    {
        entity.Note = JsonConvert.SerializeObject(state);
    }
}

public class InboundReceiptState
{
    public string ReceiptStatus { get; set; } = "Draft";
    public string? OverReceiveReason { get; set; }
    public string? WeightDiscrepancyReason { get; set; }
    public string? ExceptionDecision { get; set; } // Approve | Reject
    public string? ExceptionReason { get; set; }
    public int? ConfirmedLocationId { get; set; }
    public string? ConfirmedLocationCode { get; set; }
    public string? PutawayOverrideReason { get; set; }
    public int? QuantityEntered { get; set; }
    public string? OriginalNote { get; set; }
}
