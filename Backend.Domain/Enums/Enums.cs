namespace Backend.Domain.Enums;

public static class Enums
    {
        public enum UserStatus
        {
            /// <summary>
            /// Chưa kích hoạt
            /// </summary>
            NotActivated = 1001,
            /// <summary>
            /// Đã kích hoạt
            /// </summary>
            Actived = 1002,
            /// <summary>
            /// Bị khoá
            /// </summary>
            Locked = 1003,
            /// <summary>
            /// Ngưng hoạt động
            /// </summary>
            Deactivated = 1004
        }

        public enum Action
        {
            CREATE = 1001,
            READ,
            UPDATE,
            DELETE,
            EXPORT,
            APPROVE
        }

        public enum Gender
        {
            FEMALE = 0,
            MALE = 1,
            OTHER = 2
        }

        /// <summary>
        /// Mã menu — TÊN thành viên phải TRÙNG cột Code của bảng Menu trong DB
        /// (CustomAuthorize resolve quyền theo Code qua ISystemLookup, không theo Id số).
        /// Giá trị số = Id menu hiện tại trong DB, chỉ để tra cứu/đọc hiểu.
        /// </summary>
        public enum Menu
        {
            DASHBOARD = 1,
            USER = 4,
            USER_STATUS = 5,
            ROLE = 6,
            SYSTEM_SETTINGS = 7,          // /admin/system-config
            ACTIVITY_LOGS = 8,
            AUDIT_LOGS = 9,
            MENU_LIST = 10,
            ACTIONS = 11,
            NOTIFICATION = 18,
            NOTIFICATION_CATEGORY = 19,
            NOTIFICATION_TYPE = 20,       // /admin/notification-types
            USER_DEVICE = 27,
            USER_VERIFICATION_TOKEN = 28,
            PRODUCT = 31,                 // /admin/products
            INBOUND_ORDERS = 32,
            OUTBOUND_ORDERS = 33,         // /admin/outbound-orders
            STOCKTAKE = 34,
            REPORTS = 36,
            ALERTS = 37,
            PRODUCT_VARIANTS = 38,
            SUPPLIERS = 40,
            UNIT_OF_MEASURES = 41,
            WAREHOUSES = 42,
            PRODUCT_ATTRIBUTES = 44,      // /admin/product-attributes
            FARMERS = 45,
            CUSTOMERS = 46,
            RICE_VARIETIES = 47,
            ORGANIZATIONS = 48,
            MILLING_YIELD_CONFIGS = 49,
            STOCK_ALERT_CONFIGS = 50,
            RICE_PURCHASE = 51,
            INVENTORIES = 54,
            PRODUCT_CATEGORIES = 55,      // /admin/product-categories
            WAREHOUSE_MAP = 57,
            QUALITY_INSPECTIONS = 60,
            MILLING_ORDERS = 61,
            SALE_ORDERS = 65,
            DEBTS = 67,
            PADDY_LOTS = 69,

            // ── Bảng trạng thái (lookup) — Id số chỉ để đọc hiểu, quyền resolve theo Code = TÊN thành viên ──
            INBOUND_ORDER_STATUS = 70,
            OUTBOUND_ORDER_STATUS = 71,
            STOCK_TAKE_STATUS = 72,
            CUSTOMER_RETURN_ORDER_STATUS = 73,
            RETURN_TO_SUPPLIER_ORDER_STATUS = 74,
            PADDY_PURCHASE_SCHEDULE_STATUS = 75,
            LOT_STATUS = 76,
            MILLING_ORDER_STATUS = 77,
            STOCK_TRANSFER_STATUS = 78,
            SALES_ORDER_STATUS = 79,
            PURCHASE_ORDER_STATUS = 80,
        }

        public enum Role
        {
            ADMIN = 1001,
            USER,
        }

    // Các kiểu giao dịch phát sinh trong kho hàng
    public enum InventoryTransactionType
    {
        IMPORT = 1,    // Nhập kho
        EXPORT = 2,    // Xuất kho
        ADJUST = 3,    // Điều chỉnh
        STOCKTAKE = 4  // Kiểm kho định kỳ
    }

    public enum StockTakeStatusEnum
    {
        Draft = 1,
        Submitted = 2,
        Approved = 3,
        Rejected = 4
    }
}