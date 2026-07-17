using System;
using Backend.Application.DTOs.Alerts;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class AlertMapping
{
    public static AlertDetailDto ToDto(this Alert entity)
    {
        return new AlertDetailDto
        {
            Id = entity.Id,
            AlertType = entity.AlertType,
            Severity = entity.Severity,
            WarehouseId = entity.WarehouseId,
            WarehouseCode = entity.Warehouse?.Code,
            WarehouseName = entity.Warehouse?.Name,
            ProductVariantId = entity.ProductVariantId,
            ProductVariantSku = entity.ProductVariant?.SKU,
            ProductName = entity.ProductVariant?.Product?.Name,
            LocationId = entity.LocationId,
            LocationName = entity.Location?.ZoneName,
            Message = entity.Message,
            RelatedEntityType = entity.RelatedEntityType,
            RelatedEntityId = entity.RelatedEntityId,
            Status = entity.Status,
            AcknowledgedBy = entity.AcknowledgedBy,
            AcknowledgedByName = entity.AcknowledgedByUser != null
                ? $"{entity.AcknowledgedByUser.FirstName} {entity.AcknowledgedByUser.LastName}".Trim()
                : null,
            AcknowledgedAt = entity.AcknowledgedAt,
            ResolvedAt = entity.ResolvedAt,
            CreatedDate = entity.CreatedDate
        };
    }
}
