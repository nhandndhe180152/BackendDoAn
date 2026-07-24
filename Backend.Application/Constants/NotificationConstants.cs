using System.Collections.Generic;

namespace Backend.Application.Constants;

/// <summary>
/// Hằng số thông báo cho hệ thống quản lý chuỗi cung ứng lúa/gạo.
///
/// QUY ƯỚC (theo yêu cầu nghiệp vụ):
/// - LOẠI thông báo (NotificationType) và DANH MỤC thông báo (NotificationCategory)
///   KHÔNG hard-code Id — dispatcher phân giải theo TÊN từ dữ liệu đã lưu trong DB
///   (admin tự thêm qua trang quản trị). Xem <see cref="TypeName"/> và <see cref="Category"/>.
/// - LOẠI thông báo luôn là "Hệ thống" (<see cref="TypeName.System"/>).
/// - NỘI DUNG thông báo (tiêu đề + nội dung) được FIX CỨNG tại file constant này
///   trong <see cref="Catalog"/>, dùng placeholder {0},{1}... điền bằng string.Format khi dispatch.
///
/// Nếu DB chưa có LOẠI/DANH MỤC tương ứng (theo tên) thì dispatcher sẽ bỏ qua và ghi log —
/// vì vậy cần thêm sẵn dữ liệu bên dưới vào DB (xem danh sách seed kèm theo).
/// </summary>
public static class NotificationConstants
{
    /// <summary>Tên LOẠI thông báo — phân giải theo tên từ bảng NotificationType.</summary>
    public static class TypeName
    {
        /// <summary>Loại thông báo duy nhất hiện dùng: "Hệ thống".</summary>
        public const string System = "Hệ thống";
    }

    /// <summary>Tên DANH MỤC thông báo — phân giải theo tên từ bảng NotificationCategory.</summary>
    public static class Category
    {
        public const string LowStock = "Cảnh báo tồn kho thấp";
        public const string Purchasing = "Thu mua lúa";
        public const string PurchaseOrder = "Đơn mua hàng";
        public const string InboundOrder = "Đơn nhập kho";
        public const string Milling = "Xay xát";
        public const string SalesOrder = "Đơn bán hàng";
        public const string OutboundOrder = "Đơn xuất kho";
        public const string StockTransfer = "Điều chuyển kho";
        public const string StockTake = "Kiểm kê kho";
        public const string QualityInspection = "Kiểm định chất lượng";
        public const string System = "Hệ thống";
    }

    /// <summary>Mã sự kiện cần thông báo (đã được nối vào nghiệp vụ).</summary>
    public static class Code
    {
        // Thu mua lúa
        public const string PurchaseScheduleCreated = "PURCHASE_SCHEDULE_CREATED";
        public const string PurchaseReceiptConfirmed = "PURCHASE_RECEIPT_CONFIRMED";
        // Đơn mua hàng
        public const string PurchaseOrderConfirmed = "PURCHASE_ORDER_CONFIRMED";
        public const string PurchaseOrderCancelled = "PURCHASE_ORDER_CANCELLED";
        // Đơn nhập kho
        public const string InboundSubmitted = "INBOUND_SUBMITTED";
        public const string InboundApproved = "INBOUND_APPROVED";
        public const string InboundRejected = "INBOUND_REJECTED";
        public const string InboundReceived = "INBOUND_RECEIVED";
        // Xay xát
        public const string MillingOrderCreated = "MILLING_ORDER_CREATED";
        public const string MillingCompleted = "MILLING_COMPLETED";
        // Đơn bán hàng
        public const string SalesOrderCreated = "SALES_ORDER_CREATED";
        public const string SalesOrderConfirmed = "SALES_ORDER_CONFIRMED";
        public const string SalesOrderCancelled = "SALES_ORDER_CANCELLED";
        public const string DeliveryCompleted = "DELIVERY_COMPLETED";
        // Đơn xuất kho
        public const string OutboundDispatched = "OUTBOUND_DISPATCHED";
        // Điều chuyển kho
        public const string StockTransferConfirmed = "STOCK_TRANSFER_CONFIRMED";
        // Kiểm kê kho
        public const string StockTakeApproved = "STOCKTAKE_APPROVED";
        public const string StockTakeRejected = "STOCKTAKE_REJECTED";
        // Kiểm định chất lượng
        public const string QualityInspectionAssigned = "QUALITY_INSPECTION_ASSIGNED";
        public const string QualityInspectionResult = "QUALITY_INSPECTION_RESULT";
        // Cảnh báo
        public const string LowStockAlert = "LOW_STOCK_ALERT";
    }

    public sealed class Template
    {
        /// <summary>Tên danh mục (phân giải sang NotificationCategoryId theo tên trong DB).</summary>
        public string Category { get; init; } = NotificationConstants.Category.System;
        /// <summary>Màu gợi ý cho danh mục — chỉ dùng khi seed dữ liệu, dispatcher không ghi màu.</summary>
        public string Color { get; init; } = "#6366f1";
        public string Title { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
    }

    /// <summary>
    /// Mã -> mẫu thông báo. Nội dung dùng placeholder {0},{1}... điền bằng
    /// string.Format khi dispatch.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, Template> Catalog =
        new Dictionary<string, Template>
        {
            // ── Thu mua lúa ──────────────────────────────────────────────
            [Code.PurchaseScheduleCreated] = new()
            {
                Category = Category.Purchasing,
                Color = "#0ea5e9",
                Title = "Lịch thu mua mới",
                Content = "Lịch thu mua {0} vừa được tạo. Vui lòng chuẩn bị nhân công, xe và bao để đi thu."
            },
            [Code.PurchaseReceiptConfirmed] = new()
            {
                Category = Category.Purchasing,
                Color = "#10b981",
                Title = "Phiếu mua lúa đã xác nhận",
                Content = "Phiếu mua lúa {0} đã được xác nhận và tạo lô hàng nhập kho."
            },

            // ── Đơn mua hàng ─────────────────────────────────────────────
            [Code.PurchaseOrderConfirmed] = new()
            {
                Category = Category.PurchaseOrder,
                Color = "#10b981",
                Title = "Đơn mua hàng đã xác nhận",
                Content = "Đơn mua hàng {0} đã được xác nhận."
            },
            [Code.PurchaseOrderCancelled] = new()
            {
                Category = Category.PurchaseOrder,
                Color = "#ef4444",
                Title = "Đơn mua hàng bị hủy",
                Content = "Đơn mua hàng {0} đã bị hủy."
            },

            // ── Đơn nhập kho ─────────────────────────────────────────────
            [Code.InboundSubmitted] = new()
            {
                Category = Category.InboundOrder,
                Color = "#3b82f6",
                Title = "Phiếu nhập kho chờ duyệt",
                Content = "Phiếu nhập kho {0} vừa được gửi và đang chờ phê duyệt. Vui lòng kiểm tra và xử lý."
            },
            [Code.InboundApproved] = new()
            {
                Category = Category.InboundOrder,
                Color = "#10b981",
                Title = "Phiếu nhập kho đã được duyệt",
                Content = "Phiếu nhập kho {0} đã được phê duyệt. Bạn có thể tiến hành nhận hàng."
            },
            [Code.InboundRejected] = new()
            {
                Category = Category.InboundOrder,
                Color = "#ef4444",
                Title = "Phiếu nhập kho bị từ chối",
                Content = "Phiếu nhập kho {0} đã bị từ chối. Lý do: {1}."
            },
            [Code.InboundReceived] = new()
            {
                Category = Category.InboundOrder,
                Color = "#10b981",
                Title = "Đã nhập hàng vào kho",
                Content = "Phiếu nhập kho {0} đã được xác nhận nhận hàng vào kho."
            },

            // ── Xay xát ──────────────────────────────────────────────────
            [Code.MillingOrderCreated] = new()
            {
                Category = Category.Milling,
                Color = "#3b82f6",
                Title = "Lệnh xay mới",
                Content = "Lệnh xay {0} vừa được tạo. Vui lòng chuẩn bị lúa đầu vào."
            },
            [Code.MillingCompleted] = new()
            {
                Category = Category.Milling,
                Color = "#10b981",
                Title = "Xay xát hoàn tất",
                Content = "Lệnh xay {0} đã hoàn tất, gạo thành phẩm đã được nhập kho."
            },

            // ── Đơn bán hàng ─────────────────────────────────────────────
            [Code.SalesOrderCreated] = new()
            {
                Category = Category.SalesOrder,
                Color = "#3b82f6",
                Title = "Đơn bán mới",
                Content = "Đơn bán {0} vừa được tạo."
            },
            [Code.SalesOrderConfirmed] = new()
            {
                Category = Category.SalesOrder,
                Color = "#10b981",
                Title = "Đơn bán đã xác nhận",
                Content = "Đơn bán {0} đã được xác nhận. Vui lòng chuẩn bị hàng để giao."
            },
            [Code.SalesOrderCancelled] = new()
            {
                Category = Category.SalesOrder,
                Color = "#ef4444",
                Title = "Đơn bán bị hủy",
                Content = "Đơn bán {0} đã bị hủy."
            },
            [Code.DeliveryCompleted] = new()
            {
                Category = Category.SalesOrder,
                Color = "#10b981",
                Title = "Giao hàng hoàn tất",
                Content = "Đơn bán {0} đã được giao hàng hoàn tất."
            },

            // ── Đơn xuất kho ─────────────────────────────────────────────
            [Code.OutboundDispatched] = new()
            {
                Category = Category.OutboundOrder,
                Color = "#10b981",
                Title = "Phiếu xuất kho đã xuất hàng",
                Content = "Phiếu xuất kho thuộc đơn bán {0} đã được xác nhận xuất kho thành công."
            },

            // ── Điều chuyển kho ──────────────────────────────────────────
            [Code.StockTransferConfirmed] = new()
            {
                Category = Category.StockTransfer,
                Color = "#10b981",
                Title = "Điều chuyển kho hoàn tất",
                Content = "Phiếu điều chuyển {0} đã được xác nhận."
            },

            // ── Kiểm kê kho ──────────────────────────────────────────────
            [Code.StockTakeApproved] = new()
            {
                Category = Category.StockTake,
                Color = "#10b981",
                Title = "Phiếu kiểm kê đã được duyệt",
                Content = "Phiếu kiểm kê kho {0} đã được phê duyệt và tồn kho đã được điều chỉnh theo kết quả kiểm kê."
            },
            [Code.StockTakeRejected] = new()
            {
                Category = Category.StockTake,
                Color = "#ef4444",
                Title = "Phiếu kiểm kê bị từ chối",
                Content = "Phiếu kiểm kê kho {0} đã bị từ chối. Lý do: {1}."
            },

            // ── Kiểm định chất lượng ─────────────────────────────────────
            [Code.QualityInspectionAssigned] = new()
            {
                Category = Category.QualityInspection,
                Color = "#f59e0b",
                Title = "Được giao kiểm định chất lượng",
                Content = "Bạn được giao đi kiểm định chất lượng lô {0}. Vui lòng tiến hành kiểm tra và ghi nhận kết quả."
            },
            [Code.QualityInspectionResult] = new()
            {
                Category = Category.QualityInspection,
                Color = "#10b981",
                Title = "Có kết quả kiểm định lô",
                Content = "Lô {0} đã được kiểm định với kết quả: {1}."
            },

            // ── Cảnh báo tồn kho thấp ────────────────────────────────────
            [Code.LowStockAlert] = new()
            {
                Category = Category.LowStock,
                Color = "#ef4444",
                Title = "Cảnh báo tồn kho thấp",
                Content = "Sản phẩm \"{0}\" tại {1} chỉ còn {2}, đã xuống dưới mức tồn tối thiểu {3}. Vui lòng lên kế hoạch bổ sung hàng."
            },
        };
}
