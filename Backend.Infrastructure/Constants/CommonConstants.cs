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
            nameof(ProductAttribute),
            nameof(InboundOrder),
            nameof(InboundOrderItem),
            nameof(OutboundOrder),
            nameof(OutboundOrderItem),
            nameof(Inventory),
            nameof(InventoryTransaction),
            nameof(Supplier),
            nameof(CustomerReturnOrder),
            nameof(CustomerReturnOrderItem),
            nameof(ReturnToSupplierOrder),
            nameof(ReturnToSupplierOrderItem),
            nameof(StockAlertConfig),
            nameof(StockTake),
            nameof(StockTakeItem),
            nameof(UnitOfMeasure),
            nameof(PaddyLot)
    };

    /// <summary>
    /// Danh sách entity sẽ phát tín hiệu realtime khi dữ liệu thay đổi (qua SignalR).
    /// Chỉ là "tín hiệu đổi" để FE tự gọi lại API - KHÔNG đẩy dữ liệu.
    /// Muốn bật/tắt realtime cho bảng nào thì thêm/bớt tên ở đây (so khớp với
    /// tên class entity = GetType().Name). Độc lập với AuditedEntityNames.
    /// </summary>
    public static readonly string[] RealtimeEntityNames = new[]
    {
            "User",
            "Role",
            "Permission",
            "UserRole",
            "Menu",
            "UserStatus",
            "Action",
            "Product",
            "ProductCategory",
            "ProductVariant",
            "ProductAttribute",
            "Supplier",
            "UnitOfMeasure",
            "Warehouse",
            "Location",
            "InboundOrder",
            "Inventory",
            "InventoryTransaction",
            "StockTake",
            "StockTakeItem",
            "Alert",
            "SystemConfig",
            "Notification",
            "NotificationCategory",
            "NotificationType",
            "AuditLog",
            "ActivityLog"
    };
}

