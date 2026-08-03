namespace Backend.Application.Constants;

/// <summary>
/// Tên/Mã trạng thái đơn trả hàng về nhà cung cấp (ReturnToSupplierOrder) — khớp với ReturnToSupplierOrderStatusSeed.
/// </summary>
public static class ReturnToSupplierOrderStatusNames
{
    public const string Draft     = "DRAFT";
    public const string Approved  = "APPROVED";
    public const string Completed = "COMPLETED";
    public const string Cancelled = "CANCELLED";
}
