namespace Backend.Application.Constants;

/// <summary>
/// Loại lô hàng trong hệ thống — dùng trong PaddyLot.LotType.
/// </summary>
public static class LotTypeConstants
{
    /// <summary>Lúa mua từ nông dân/ngoài</summary>
    public const string Paddy         = "PADDY";

    /// <summary>Gạo thành phẩm sau xay</summary>
    public const string Rice          = "RICE";

    /// <summary>Phụ phẩm: tấm, cám, trấu</summary>
    public const string ByProduct     = "BYPRODUCT";

    /// <summary>Hàng mua ngoài (non-paddy): bao bì, vật tư, hàng mua lại</summary>
    public const string PurchasedGood = "PURCHASED_GOOD";
}
