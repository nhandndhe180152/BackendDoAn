using System;
using Backend.Application.DTOs.InventoryTransactions;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class InventoryTransactionMapping
{
    public static InventoryTransactionDto ToDto(this InventoryTransaction entity)
    {
        var locationCode = entity.Location == null
            ? null
            : string.Join("-",
                new[]
                {
                    entity.Location.ZoneName,
                    entity.Location.ShelfRow,
                    entity.Location.ShelfLevel,
                    entity.Location.SlotCode
                }.Where(x => !string.IsNullOrWhiteSpace(x)));

        return new InventoryTransactionDto
        {
            Id = entity.Id,
            InventoryId = entity.InventoryId,
            WarehouseId = entity.WarehouseId,
            WarehouseName = entity.Warehouse?.Name,
            LocationId = entity.LocationId,
            LocationCode = locationCode,
            ProductVariantId = entity.ProductVariantId,
            SKU = entity.ProductVariant?.SKU,
            ProductVariantName = entity.ProductVariant?.Name,
            ProductName = entity.ProductVariant?.Product?.Name,
            TransactionType = entity.TransactionType,
            ReferenceType = entity.ReferenceType,
            ReferenceId = entity.ReferenceId,
            ReferenceItemId = entity.ReferenceItemId,
            Quantity = entity.Quantity,
            BeforeQuantity = entity.BeforeQuantity,
            AfterQuantity = entity.AfterQuantity,
            WeightKg = entity.WeightKg,
            IotWeightLogId = entity.IotWeightLogId,
            Note = entity.Note,
            CreatedDate = entity.CreatedDate,
            CreatedBy = entity.CreatedBy
        };
    }
}
