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

        public enum Menu
        {
            DASHBOARD = 1,
            REPORT,
            USER = 4,
            USER_STATUS = 5,
            ROLE = 6,
            SYSTEM_SETTINGS = 7,
            MENU_LIST = 10,
            NOTIFICATION_CATEGORY = 19,
            SYSTEM_CONFIG,
            ACTIVITY_LOGS = 8,
            NOTIFICATION = 18,
            NOTIFICATION_TYPE = 20,
            PRODUCT = 30,
            PRODUCT_CATEGORY = 31,
            PRODUCT_VARIANT = 32,
            PRODUCT_ATTRIBUTE = 33,
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