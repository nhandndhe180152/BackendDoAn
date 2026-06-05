using System;
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
    }

    public static class Role
    {
        public const int ADMIN = 1001;
        public const int END_USER = 1002;
        public const int DRIVER = 1003;
        public const int DISPATCHER = 1004;
        public const int EXECUTIVE = 1005;
    }

    public static readonly HashSet<int> ListRoleRegister = new()
        {
            Role.DISPATCHER
        };

    public static readonly HashSet<int> ListRoleForOffice = new()
        {
            Role.DISPATCHER,
        };

    public static readonly HashSet<int> ListRoleForUserManagement = new()
        {
            Role.ADMIN,
            Role.END_USER,
            Role.DISPATCHER,
            Role.EXECUTIVE
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

    public static class NotificationCategory
    {
        public const int TRIP_REQUEST = 1001;
        public const int TRIP = 1002;
        public const int FUEL_LOG = 1003;
        public const int TRIP_EXPENSE = 1004;
        public const int MAINTENANCE_RECORD = 1005;
    }
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

    public static readonly Dictionary<string, string> EntityDisplayMap = new()
        {
            { "User", "Người dùng" },
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
