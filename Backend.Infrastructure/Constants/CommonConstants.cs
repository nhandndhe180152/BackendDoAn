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
            // ── Người dùng & phân quyền ──────────────────────────────────
            "User",
            "Role",
            "Permission",
            "UserRole",
            "Menu",
            "UserStatus",
            "Action",
            "UserDevice",

            // ── Danh mục dùng chung ──────────────────────────────────────
            "Product",
            "ProductCategory",
            "ProductVariant",
            "ProductAttribute",
            "Supplier",
            "Customer",
            "Farmer",
            "RiceVariety",
            "Organization",
            "UnitOfMeasure",
            "Warehouse",
            "Location",

            // ── Nhập kho ─────────────────────────────────────────────────
            "InboundOrder",
            "InboundOrderItem",
            "PurchaseOrder",
            "PurchaseOrderItem",

            // ── Thu mua lúa ──────────────────────────────────────────────
            "PaddyPurchaseSchedule",
            "PaddyPurchaseReceipt",

            // ── Lô lúa & chất lượng ──────────────────────────────────────
            "PaddyLot",
            "PaddyLotBag",
            "PaddyLotBagContent",
            "PaddyLotBagMovement",
            "QualityInspection",

            // ── Tồn kho ──────────────────────────────────────────────────
            "Inventory",
            "InventoryTransaction",
            "StockTake",
            "StockTakeItem",
            "StockTransfer",
            "StockTransferItem",

            // ── Bán hàng & xuất kho ──────────────────────────────────────
            "SalesOrder",
            "SalesOrderItem",
            "OutboundOrder",
            "OutboundOrderItem",
            "OutboundOrderItemAllocation",
            "DeliveryNote",

            // ── Xay xát ──────────────────────────────────────────────────
            "MillingOrder",
            "MillingOrderInput",
            "MillingOrderOutput",

            // ── Trả hàng ─────────────────────────────────────────────────
            "CustomerReturnOrder",
            "CustomerReturnOrderItem",
            "ReturnToSupplierOrder",
            "ReturnToSupplierOrderItem",

            // ── Công nợ ──────────────────────────────────────────────────
            "PartyDebt",
            "DebtTransaction",

            // ── Cấu hình & cảnh báo ──────────────────────────────────────
            "Alert",
            "StockAlertConfig",
            "MillingYieldConfig",
            "SystemConfig",

            // ── Thông báo ────────────────────────────────────────────────
            "Notification",
            "UserNotification",
            "NotificationCategory",
            "NotificationType",

            // ── Bảng trạng thái (màn quản trị trạng thái) ────────────────
            "InboundOrderStatus",
            "OutboundOrderStatus",
            "SalesOrderStatus",
            "PurchaseOrderStatus",
            "StockTakeStatus",
            "StockTransferStatus",
            "MillingOrderStatus",
            "CustomerReturnOrderStatus",
            "ReturnToSupplierOrderStatus",
            "PaddyPurchaseScheduleStatus",
            "LotStatus",

            // ── Nhật ký ──────────────────────────────────────────────────
            "AuditLog",
            "ActivityLog"
    };
}

