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
            BLOG_POST = 15,
            BLOG_POST_LIST,
            BLOG_POST_DETAIL,
            BLOG_POST_CATEGORY = 13,
            BLOG_POST_LAYOUT = 14,
            BLOG_POST_STATUS = 16,
            BLOG_POST_TAG,
            USER = 4,
            USER_LIST,
            USER_STATUS = 5,
            ROLE = 6,
            SYSTEM_SETTINGS = 7,
            MENU_LIST = 10,
            NOTIFICATION_CATEGORY = 19,
            TAG = 25,
            SYSTEM_CONFIG,
            ACTIVITY_LOGS = 8,
            TAG_TYPE = 26,
            NOTIFICATION = 18,
            NOTIFICATION_LIST,
            NOTIFICATION_TYPE = 20,
            PROVINCE = 29,
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

    // Cấu hình phân loại lệnh gửi tới thiết bị cân IoT
    public enum IotCommandType
    {
        TARE = 1,       // Trừ bì
        RESET = 2,      // Khởi động lại thiết bị
        CALIBRATE = 3   // Hiệu chuẩn cân
    }

    public enum IotCommandStatus
    {
        Pending = 1,
        PickedUp = 2,
        Executed = 3,
        Failed = 4,
        Expired = 5
    }

    // Các kiểu giao dịch phát sinh trong kho hàng
    public enum InventoryTransactionType
    {
        IMPORT = 1,    // Nhập kho
        EXPORT = 2,    // Xuất kho
        ADJUST = 3,    // Điều chỉnh
        STOCKTAKE = 4  // Kiểm kho định kỳ
    }
}