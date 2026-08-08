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

    public static class MillingOrderStatus
    {
        public const string Draft = "DRAFT";
        public const string Reserved = "RESERVED";
        public const string InProgress = "IN_PROGRESS";
        public const string Completed = "COMPLETED";
        public const string Cancelled = "CANCELLED";
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
        public const string Auditor = "AUDITOR";       // Kiểm toán viên (chỉ đọc + xuất báo cáo/log)

        // Legacy roles (from stashed changes)
        public const string EndUser = "END_USER";
        public const string Driver = "DRIVER";
        public const string Dispatcher = "DISPATCHER";
        public const string Executive = "EXECUTIVE";
        public const string WarehouseOwner = "WAREHOUSE_OWNER";
        public const string WarehouseStaff = "WAREHOUSE_STAFF";
        public const string SalesStaff = "SALES_STAFF";
    }

    public static class Action
    {
        public const string Create = "CREATE";
        public const string Read = "READ";
        public const string Update = "UPDATE";
        public const string Delete = "DELETE";
        public const string Export = "EXPORT";
        public const string Approve = "APPROVE";
        public const string Confirm = "CONFIRM";
        public const string Cancel = "CANCEL";
        public const string Preview = "PREVIEW";
        public const string Inspect = "INSPECT";
    }

    public static class PartyType
    {
        public const string Customer = "CUSTOMER";
        public const string Supplier = "SUPPLIER";
        public const string Farmer = "FARMER";
    }

    public static class DebtDirection
    {
        public const string Receivable = "RECEIVABLE";
        public const string Payable = "PAYABLE";
    }

    public static class DebtTransactionType
    {
        public const string Charge = "CHARGE";
        public const string ReturnCredit = "RETURN_CREDIT";
        public const string RefundPayable = "REFUND_PAYABLE";
        public const string SaleCharge = "SALE_CHARGE";
        public const string Payment = "PAYMENT";
    }

    public static class PaddyPurchaseScheduleStatus
    {
        public const string New = "NEW";
        public const string Confirmed = "CONFIRMED";
        public const string Collecting = "COLLECTING";
        public const string Weighed = "WEIGHED";
        public const string Stocked = "STOCKED";
        public const string Cancelled = "CANCELLED";
        public const string PartiallyStocked = "PARTIALLY_STOCKED";
    }

    public static class SalesOrderChannel
    {
        public const string Direct = "DIRECT";
        public const string Wholesale = "WHOLESALE";
    }
}
