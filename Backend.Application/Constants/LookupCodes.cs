namespace Backend.Application.Constants;

/// <summary>
/// Mã (Code) định danh ổn định của các bảng lookup hệ thống — hợp đồng DUY NHẤT giữa code và DB.
/// Giá trị phải khớp cột Code trong seed/backfill. KHÔNG dùng Id số để so sánh nữa.
/// (Cố ý khớp với tên thành viên enum cũ trong Enums.cs để chuyển đổi mượt.)
/// </summary>
public static class LookupCodes
{
    public static class UserStatus
    {
        public const string NotActivated = "NotActivated";
        public const string Active = "Actived";
        public const string Locked = "Locked";
        public const string Deactivated = "Deactivated";
    }

    public static class StockTakeStatus
    {
        public const string Draft = "Draft";
        public const string Submitted = "Submitted";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
    }

    public static class Role
    {
        public const string Admin = "ADMIN";
        public const string EndUser = "END_USER";
        public const string Driver = "DRIVER";
        public const string Dispatcher = "DISPATCHER";
        public const string Executive = "EXECUTIVE";
    }

    public static class Action
    {
        public const string Create = "CREATE";
        public const string Read = "READ";
        public const string Update = "UPDATE";
        public const string Delete = "DELETE";
        public const string Export = "EXPORT";
        public const string Approve = "APPROVE";
    }
}
