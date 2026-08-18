using System;
using System.Linq;
using Backend.Application.DTOs.Inventories;
using Backend.Application.Constants;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class InventoryMapping
{
    public static InventoryDto ToDto(this Inventory entity)
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

        var isQuarantined = (entity.Location != null && entity.Location.IsQuarantine)
            || (entity.PaddyLot != null && entity.PaddyLot.Status?.Code == LotStatusCodeConstants.Quarantine);
        var isSellable = !isQuarantined && (entity.PaddyLotId == null || (entity.PaddyLot != null && entity.PaddyLot.Status != null && entity.PaddyLot.Status.IsSellable));
        var otherBlocked = !isQuarantined && !isSellable;

        return new InventoryDto
        {
            Id = entity.Id,
            WarehouseId = entity.WarehouseId,
            WarehouseName = entity.Warehouse?.Name,
            LocationId = entity.LocationId,
            LocationCode = locationCode,
            ProductVariantId = entity.ProductVariantId,
            SKU = entity.ProductVariant?.SKU,
            ProductVariantName = entity.ProductVariant?.Name,
            ProductId = entity.ProductVariant?.ProductId ?? 0,
            ProductName = entity.ProductVariant?.Product?.Name,
            CostPrice = entity.CostPrice,
            QuantityOnHand = entity.QuantityOnHand,
            QuantityReserved = entity.QuantityReserved,
            QuantityAvailable = entity.QuantityAvailable,
            QuarantinedKg = isQuarantined ? entity.QuantityOnHand : 0m,
            SellableOnHandKg = isSellable ? entity.QuantityOnHand : 0m,
            OtherBlockedKg = otherBlocked ? entity.QuantityOnHand : 0m,
            MinStockLevel = entity.ProductVariant?.MinStockLevel,
            IsLowStock = entity.ProductVariant?.MinStockLevel != null &&
                         entity.QuantityOnHand <= entity.ProductVariant.MinStockLevel,
            LastStockTakeDate = entity.LastStockTakeDate,
            CreatedDate = entity.CreatedDate,
            LastModifiedDate = entity.LastModifiedDate
        };
    }
}
