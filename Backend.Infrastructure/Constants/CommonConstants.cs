using System;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Constants;

public static class CommonConstants
{
    public static readonly string[] AuditedEntityNames = new[]
    {
            nameof(User),
            nameof(Warehouse),
            nameof(Location),
            nameof(Product),
            nameof(ProductCategory),
            nameof(ProductVariant),
            nameof(InboundOrder),
            nameof(InboundOrderItem),
            nameof(OutboundOrder),
            nameof(OutboundOrderItem),
            nameof(Inventory),
            nameof(InventoryTransaction),
            nameof(IotDevice),
            nameof(IotDeviceCommand),
            nameof(Supplier),
            nameof(CustomerReturnOrder),
            nameof(CustomerReturnOrderItem),
            nameof(ReturnToSupplierOrder),
            nameof(ReturnToSupplierOrderItem),
            nameof(StockAlertConfig),
            nameof(StockTake),
            nameof(StockTakeItem),
            nameof(UnitOfMeasure)
    };
}

