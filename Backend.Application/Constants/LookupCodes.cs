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
        // Bộ vai trò theo tài liệu nghiệp vụ (Report 1 - Vision & Scope, Table 6):
        // owner, purchasing, warehouse, milling, sales (+ admin kỹ thuật).
        public const string Admin = "ADMIN";           // Quản trị viên hệ thống
        public const string Owner = "OWNER";           // Chủ kho / chủ hộ kinh doanh
        public const string Purchasing = "PURCHASING"; // Nhân viên thu mua
        public const string Warehouse = "WAREHOUSE";   // Nhân viên kho
        public const string Milling = "MILLING";       // Nhân viên xay xát
        public const string Sales = "SALES";           // Nhân viên bán hàng
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
