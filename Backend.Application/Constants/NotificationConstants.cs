using System.Collections.Generic;

namespace Backend.Application.Constants;

/// <summary>
/// Hằng số thông báo cho hệ thống quản lý kho (StockLite WMS).
/// - Danh mục dùng theo TÊN; dispatcher tự tạo trong DB nếu chưa có.
/// - Catalog ánh xạ mã sự kiện -> mẫu tiêu đề/nội dung + danh mục, theo các
///   sự kiện cần thông báo trong tài liệu (Low Stock, Inbound Bottleneck,
///   duyệt Inbound/Outbound, sai lệch trả hàng).
/// </summary>
public static class NotificationConstants
{
    public static class Category
    {
        public const string LowStock = "Cảnh báo tồn kho thấp";
        public const string InboundBottleneck = "Cảnh báo nghẽn nhập kho";
        public const string InboundOrder = "Đơn nhập kho";
        public const string OutboundOrder = "Đơn xuất kho";
        public const string ReturnOrder = "Đơn trả hàng";
        public const string StockTake = "Kiểm kê kho";
        public const string System = "Hệ thống";
    }

    /// <summary>Mã sự kiện cần thông báo.</summary>
    public static class Code
    {
        public const string LowStockAlert = "LOW_STOCK_ALERT";
        public const string InboundBottleneck = "INBOUND_BOTTLENECK";
        public const string InboundSubmitted = "INBOUND_SUBMITTED";
        public const string InboundApproved = "INBOUND_APPROVED";
        public const string InboundRejected = "INBOUND_REJECTED";
        public const string OutboundSubmitted = "OUTBOUND_SUBMITTED";
        public const string OutboundApproved = "OUTBOUND_APPROVED";
        public const string OutboundRejected = "OUTBOUND_REJECTED";
        public const string ReturnDiscrepancy = "RETURN_DISCREPANCY";
    }

    public sealed class Template
    {
        public string Category { get; init; } = NotificationConstants.Category.System;
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
            [Code.LowStockAlert] = new()
            {
                Category = Category.LowStock,
                Color = "#ef4444",
                Title = "Cảnh báo tồn kho thấp",
                Content = "Sản phẩm \"{0}\" tại {1} còn {2} (dưới mức tối thiểu {3})."
            },
            [Code.InboundBottleneck] = new()
            {
                Category = Category.InboundBottleneck,
                Color = "#f59e0b",
                Title = "Cảnh báo nghẽn nhập kho",
                Content = "Khối lượng nhập tại {0} đang ở mức {1} so với năng lực cho phép ({2})."
            },
            [Code.InboundSubmitted] = new()
            {
                Category = Category.InboundOrder,
                Color = "#3b82f6",
                Title = "Phiếu nhập chờ duyệt",
                Content = "Phiếu nhập {0} vừa được gửi và đang chờ phê duyệt."
            },
            [Code.InboundApproved] = new()
            {
                Category = Category.InboundOrder,
                Color = "#10b981",
                Title = "Phiếu nhập đã được duyệt",
                Content = "Phiếu nhập {0} đã được phê duyệt."
            },
            [Code.InboundRejected] = new()
            {
                Category = Category.InboundOrder,
                Color = "#ef4444",
                Title = "Phiếu nhập bị từ chối",
                Content = "Phiếu nhập {0} đã bị từ chối. Lý do: {1}."
            },
            [Code.OutboundSubmitted] = new()
            {
                Category = Category.OutboundOrder,
                Color = "#3b82f6",
                Title = "Phiếu xuất chờ duyệt",
                Content = "Phiếu xuất {0} vừa được gửi và đang chờ phê duyệt."
            },
            [Code.OutboundApproved] = new()
            {
                Category = Category.OutboundOrder,
                Color = "#10b981",
                Title = "Phiếu xuất đã được duyệt",
                Content = "Phiếu xuất {0} đã được phê duyệt."
            },
            [Code.OutboundRejected] = new()
            {
                Category = Category.OutboundOrder,
                Color = "#ef4444",
                Title = "Phiếu xuất bị từ chối",
                Content = "Phiếu xuất {0} đã bị từ chối. Lý do: {1}."
            },
            [Code.ReturnDiscrepancy] = new()
            {
                Category = Category.ReturnOrder,
                Color = "#f59e0b",
                Title = "Sai lệch khi trả hàng",
                Content = "Phát hiện sai lệch ở đơn trả hàng {0}: {1}."
            },
        };
}
