using System;

namespace Backend.Application.Constants;

public static class PaddyLotBagStatuses
{
    public const string Pending = "Pending";
    public const string Stored = "Stored";
    public const string InTransit = "InTransit";
    public const string Consumed = "Consumed";
    public const string Reversed = "Reversed";
}

public static class PaddyLotBagKinds
{
    public const string Purchase = "Purchase";
    public const string Finished = "Finished";
    public const string Quarantine = "Quarantine";
}

public static class PaddyLotBagMovementTypes
{
    public const string Putaway = "Putaway";
    public const string ReceiptReversal = "ReceiptReversal";
    public const string TransferDispatch = "TransferDispatch";
    public const string TransferReceive = "TransferReceive";
    public const string MillingConsume = "MillingConsume";
    public const string MillingPack = "MillingPack";
    public const string OutboundConsume = "OutboundConsume";
    public const string DeliveryRestock = "DeliveryRestock";
    public const string CustomerReturn = "CustomerReturn";
    public const string QualityQuarantineSplit = "QualityQuarantineSplit";

    /// <summary>Bao không tìm thấy khi kiểm kê → rút khỏi kho.</summary>
    public const string StockTakeMissing = "StockTakeMissing";

    /// <summary>Bao đếm được nhưng cân lệch so với sổ sách → chỉnh khối lượng.</summary>
    public const string StockTakeAdjust = "StockTakeAdjust";

    /// <summary>Bao đếm được ngoài danh sách snapshot → nhập bổ sung.</summary>
    public const string StockTakeFound = "StockTakeFound";
}

/// <summary>
/// Tình trạng chất lượng ghi nhận khi kiểm kê từng bao/dòng.
/// Mọi giá trị khác "OK" đều coi là KHÔNG ĐẠT và sẽ đề xuất cách ly lô.
/// </summary>
public static class StockTakeQualityStatuses
{
    public const string Ok = "OK";
    public const string Wet = "WET";          // ẩm/mốc
    public const string Pest = "PEST";        // mọt, côn trùng
    public const string TornBag = "TORN_BAG"; // rách bao, đổ vãi
    public const string Other = "OTHER";

    public static readonly string[] All = { Ok, Wet, Pest, TornBag, Other };

    public static bool IsFailed(string? status) =>
        !string.IsNullOrWhiteSpace(status) &&
        !string.Equals(status, Ok, StringComparison.OrdinalIgnoreCase);
}
