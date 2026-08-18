using System;
using Backend.Application.Common;
using Backend.Share.Entities;

namespace Backend.Application.Constants;

public static class CommonConstants
{
    public const int ADMIN_USER = 1001;

    public static class MenuType
    {
        public const string ADMIN = "ADMIN";
        public const string CLIENT_HEADER = "CLIENT_HEADER";
        public const string CLIENT_FOOTER = "CLIENT_FOOTER";
    }

    public static readonly HashSet<DataItem<string>> ListMenuType = new()
        {
            new DataItem<string>{
                Id=MenuType.ADMIN,
                Name="Menu danh cho trang admin"
            },
            new DataItem<string>{
               Id=MenuType.CLIENT_HEADER,
               Name = "Header cho trang client"
            },
            new DataItem<string>{
               Id=MenuType.CLIENT_FOOTER,
               Name = "Footer cho trang client"
            }
        };

    public static class Cache
    {
        public const string PERMISSIONS_ALL_KEY = "Permissions:All";
        public const string SYSTEMCONFIG_ALL_KEY = "SystemConfig:All";
    }

    public static class Action
    {
        public const int CREATE = 1001;
        public const int READ = 1002;
        public const int UPDATE = 1003;
        public const int DELETE = 1004;
        public const int EXPORT = 1005;
        public const int APPROVE = 1006;
    }

    public static class ActionCode
    {
        public const string CREATE = "CREATE";
        public const string UPDATE = "UPDATE";
        public const string DELETE = "DELETE";
        public const string EXPORT = "EXPORT";
        public const string REJECT = "REJECT";
        public const string APPROVE = "APPROVE";
    }

    public static readonly Dictionary<string, string> ListAction = new()
        {
            //{ "CREATE", "Tạo mới" },
            //{ "UPDATE", "Cập nhật" },
            //{ "DELETE", "Xóa" },
            //{ "EXPORT", "Xuất dữ liệu" },
            //{ "REJECT", "Từ chối" },
            //{ "APPROVE", "Duyệt" },
            {"Added","Thêm mới"},
            {"Modified","Cập nhật"},
            {"Deleted","Xoá"}
        };

    public static class SystemConfig
    {
        public const string USER_MANUAL_KEY = "USER_MANUAL";
        public const string PRIVACY_POLIVY_KEY = "PRIVACY_POLICY";
        public const string TERMS_OF_SERVICE_KEY = "TERMS_OF_SERVICE";

        public const string HOT_LINE_KEY = "HOT_LINE";
        public const string ADDRESS_KEY = "ADDRESS";
        public const string EMAIL_KEY = "EMAIL";
        public const string LOGO_KEY = "LOGO";
        public const string GOOGLE_MAPS_LINK_KEY = "GOOGLE_MAPS_LINK";
        public const string WORKING_HOURS_KEY = "WORKING_HOURS";

        // ── Trọng số Smart Put-away (SCR-08) ─────────────────────────────────────
        // KHÔNG tách bảng: lưu thẳng vào SystemConfig, ConfigValue = số thực trong [0..1].
        // InboundOrderService.GetPutawaySuggestionsAsync đọc 4 key này; RÀNG BUỘC: cả 4 trọng số ≥ 0 và TỔNG = 1.0 (sai lệch tối đa 0.001).
        // Ghi đè theo từng kho: nối ":{warehouseId}" (vd "PutawayCategoryMatchWeight:5"),
        // thiếu → fallback về key global (không hậu tố) → mặc định code 0.40 / 0.30 / 0.20 / 0.10.
        public const string PUTAWAY_CATEGORY_MATCH_WEIGHT_KEY = "PutawayCategoryMatchWeight";
        public const string PUTAWAY_CAPACITY_FIT_WEIGHT_KEY = "PutawayCapacityFitWeight";
        public const string PUTAWAY_OCCUPANCY_WEIGHT_KEY = "PutawayOccupancyWeight";
        public const string PUTAWAY_PRIORITY_WEIGHT_KEY = "PutawayPriorityWeight";
        public const string PUTAWAY_SAME_PRODUCT_SCORE_KEY = "PutawaySameProductScore";
        public const string PUTAWAY_EMPTY_COLUMN_SCORE_KEY = "PutawayEmptyColumnScore";

        // Mặc định cho Smart Put-away
        public const decimal DEFAULT_PUTAWAY_CATEGORY_MATCH_WEIGHT = 0.20m;
        public const decimal DEFAULT_PUTAWAY_CAPACITY_FIT_WEIGHT = 0.40m;
        public const decimal DEFAULT_PUTAWAY_OCCUPANCY_WEIGHT = 0.30m;
        public const decimal DEFAULT_PUTAWAY_PRIORITY_WEIGHT = 0.10m;
        public const decimal DEFAULT_PUTAWAY_SAME_PRODUCT_SCORE = 1.00m;
        public const decimal DEFAULT_PUTAWAY_EMPTY_COLUMN_SCORE = 0.60m;

        // ── Bật/tắt quy tắc cảnh báo (SCR-21) ────────────────────────────────────
        // KHÔNG tách bảng: lưu thẳng vào SystemConfig giống Put-away.
        // Key = "AlertRuleEnabled:{ruleCode}" (vd "AlertRuleEnabled:LOW_STOCK"), ConfigValue = "true"/"false".
        // Thiếu row → mặc định code = BẬT (true). AlertService đọc/ghi qua ISystemConfigService.
        public const string ALERT_RULE_ENABLED_KEY = "AlertRuleEnabled";
    }

    /// <summary>
    /// Id role được PHÂN GIẢI TỪ CODE ổn định lúc chạy (không còn hard-code Id số).
    /// DB đổi Id/re-seed vẫn đúng. Fallback = Id seed cũ để không vỡ khi role chưa seed/warmup chưa chạy.
    /// </summary>
    // Vai trò theo tài liệu nghiệp vụ (Report 1 - Vision & Scope, Table 6):
    // owner, purchasing, warehouse, milling, sales (+ admin kỹ thuật).
    // Id được phân giải TỪ CODE lúc chạy (không hard-code Id số). Fallback = Id seed dự phòng.
    public static class Role
    {
        public static int ADMIN => Lookup.RoleIdOrDefault(LookupCodes.Role.Admin, 1001);
        public static int OWNER => Lookup.RoleIdOrDefault(LookupCodes.Role.Owner, 1002);
        public static int PURCHASING => Lookup.RoleIdOrDefault(LookupCodes.Role.Purchasing, 1007);
        public static int WAREHOUSE => Lookup.RoleIdOrDefault(LookupCodes.Role.Warehouse, 1008);
        public static int MILLING => Lookup.RoleIdOrDefault(LookupCodes.Role.Milling, 1009);
        public static int SALES => Lookup.RoleIdOrDefault(LookupCodes.Role.Sales, 1010);
        public static int AUDITOR => Lookup.RoleIdOrDefault(LookupCodes.Role.Auditor, 1011);

        /// <summary>
        /// Các Code role hệ thống mà code backend đang tham chiếu — dùng để bảo vệ khỏi bị xoá.
        /// Không được xoá các role này vì sẽ làm hỏng gửi thông báo theo role & phân quyền.
        /// </summary>
        public static readonly HashSet<string> SystemCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            LookupCodes.Role.Admin,
            LookupCodes.Role.Owner,
            LookupCodes.Role.Purchasing,
            LookupCodes.Role.Warehouse,
            LookupCodes.Role.Milling,
            LookupCodes.Role.Sales,
            LookupCodes.Role.Auditor,
        };
    }

    /// <summary>Các vai trò nghiệp vụ có thể gán khi tạo tài khoản nhân viên (không gồm ADMIN).</summary>
    public static readonly HashSet<int> ListRoleRegister = new()
        {
            Role.OWNER,
            Role.PURCHASING,
            Role.WAREHOUSE,
            Role.MILLING,
            Role.SALES,
            Role.AUDITOR,
        };

    /// <summary>Các vai trò làm việc nội bộ (dùng hệ thống quản lý).</summary>
    public static readonly HashSet<int> ListRoleForOffice = new()
        {
            Role.OWNER,
            Role.PURCHASING,
            Role.WAREHOUSE,
            Role.MILLING,
            Role.SALES,
            Role.AUDITOR,
        };

    public static readonly HashSet<int> ListRoleForUserManagement = new()
        {
            Role.ADMIN,
            Role.OWNER,
            Role.PURCHASING,
            Role.WAREHOUSE,
            Role.MILLING,
            Role.SALES,
            Role.AUDITOR,
        };

    public static class UserVerificationTokenPurpose
    {
        public const string ACCOUNT_ACTIVATION = "ACCOUNT_ACTIVATION";
        public const string RESET_PASSWORD = "RESET_PASSWORD";
        public const string FORGOT_PASSWORD = "FORGOT_PASSWORD";

    }

    public static class TagType
    {
        public const int TAG_TYPE_BLOG = 1001;
    }

    public static class ProductCategory
    {
        public const int Paddy = 101;
        // Seeded in DB: 101 (Lúa thô), 102 (Gạo thành phẩm), 103 (Phụ phẩm)
    }

    public static class ActivityLogType
    {
        public const string REQUEST = "REQUEST";
        public const string AUDIT = "AUDIT";
    }

    public static readonly Dictionary<string, string> ProvinceTypes = new()
        {
            {
                "city","Thành phố Trung ương"
            },
            {
                "province","Tỉnh"
            }
        };

    public static readonly Dictionary<string, string> WardTypes = new()
        {
            {
                "ward","Phường"
            },
            {
                "commune","Xã"
            }
        };

    public static class ReportPeriod
    {
        public const string LAST_7_DAY = "LAST_7_DAY";
        public const string TODAY = "TODAY";
        public const string WEEK = "WEEK";
        public const string MONTH = "MONTH";
        public const string YEAR = "YEAR";
    }

    // Đã gỡ các danh mục thông báo cũ (trip/fuel/maintenance) không phù hợp WMS.
    // Danh mục + mã sự kiện của hệ thống kho được định nghĩa trong
    // NotificationConstants (theo TÊN danh mục + Catalog code -> mẫu nội dung).
    public static class NotificationType
    {
        public const int SYSTEM = 1001;
    }

    public static class BlogPostStatus
    {
        public const int DRAFT = 1001;
        public const int SCHEDULED = 1002;
        public const int PUBLISHED = 1003;
        public const int INACTIVE = 1004;
    }
    public static readonly HashSet<DetailStatusDto<int>> ApprovalStatuses = new()
        {
            new DetailStatusDto<int>{
                Id=0,
                Name="Chờ duyệt",
                Color="#1B84FF"
            },
            new DetailStatusDto<int>{
                Id=1,
                Name="Đã duyệt",
                Color="#17C653"
            },
            new DetailStatusDto<int>{
                Id=2,
                Name="Từ chối",
                Color="#F8285A"
            }
        };

    public static readonly List<string> MonthNames = new List<string>
        {
            "Tháng 01", "Tháng 02", "Tháng 03", "Tháng 04",
            "Tháng 05", "Tháng 06", "Tháng 07", "Tháng 08",
            "Tháng 09", "Tháng 10", "Tháng 11", "Tháng 12"
        };

    // Phải khớp với AuditedEntityNames (Backend.Infrastructure) để dropdown lọc
    // "Đối tượng" của audit log hiển thị đủ tất cả loại đối tượng được ghi log.
    public static readonly Dictionary<string, string> EntityDisplayMap = new()
        {
            { "User", "Người dùng" },
            { "Warehouse", "Kho" },
            { "Location", "Vị trí lưu trữ" },
            { "PaddyLot", "Lô hàng" },
            { "Product", "Sản phẩm" },
            { "ProductCategory", "Danh mục sản phẩm" },
            { "ProductVariant", "Biến thể sản phẩm" },
            { "InboundOrder", "Đơn nhập kho" },
            { "InboundOrderItem", "Chi tiết đơn nhập" },
            { "OutboundOrder", "Đơn xuất kho" },
            { "OutboundOrderItem", "Chi tiết đơn xuất" },
            { "Inventory", "Tồn kho" },
            { "InventoryTransaction", "Giao dịch kho" },
            { "Supplier", "Nhà cung cấp" },
            { "CustomerReturnOrder", "Đơn trả hàng của khách" },
            { "CustomerReturnOrderItem", "Chi tiết đơn trả hàng của khách" },
            { "ReturnToSupplierOrder", "Đơn trả nhà cung cấp" },
            { "ReturnToSupplierOrderItem", "Chi tiết đơn trả nhà cung cấp" },
            { "StockAlertConfig", "Cấu hình cảnh báo tồn kho" },
            { "StockTake", "Kiểm kê kho" },
            { "StockTakeItem", "Chi tiết kiểm kê kho" },
            { "UnitOfMeasure", "Đơn vị tính" },
        };

    public static readonly HashSet<DetailStatusDto<int>> DriverSalaryStatuses = new()
        {
            new DetailStatusDto<int>{
                Id=0,
                Name="Chờ duyệt",
                Color="#1B84FF"
            },
            new DetailStatusDto<int>{
                Id=1,
                Name="Đã duyệt",
                Color="#17C653"
            },
            new DetailStatusDto<int>{
                Id=2,
                Name="Từ chối",
                Color="#F8285A"
            },
            new DetailStatusDto<int>{
                Id=3,
                Name="Đã thanh toán",
                Color="#7239EA"
            }
        };
}
