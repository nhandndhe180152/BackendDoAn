namespace Backend.Application.Constants;

public static class PaddyLotBagStatuses
{
    public const string Pending = "Pending";
    public const string Stored = "Stored";
    public const string InTransit = "InTransit";
    public const string OutboundStaging = "OutboundStaging";
    public const string Consumed = "Consumed";
    public const string Reversed = "Reversed";
    /// <summary>Bao bị trả lại sau kiểm định chất lượng (Disposition = REJECT_RETURN).</summary>
    public const string RejectedReturn = "RejectedReturn";
    /// <summary>Bao hỏng, bị loại bỏ khỏi kho khi kiểm kê (bỏ nguyên bao).</summary>
    public const string Disposed = "Disposed";
}

public static class PaddyLotBagKinds
{
    public const string Purchase = "Purchase";
    public const string Finished = "Finished";
    public const string Quarantine = "Quarantine";
    /// <summary>Bao thuộc lô bị trả lại sau kiểm định.</summary>
    public const string RejectedReturn = "RejectedReturn";
}

public static class PaddyLotBagMovementTypes
{
    public const string Putaway = "Putaway";
    public const string ReceiptReversal = "ReceiptReversal";
    public const string TransferDispatch = "TransferDispatch";
    public const string TransferReceive = "TransferReceive";
    /// <summary>Chuyển kho: bao không đạt chất lượng được đưa vào ô cách ly ở kho nguồn.</summary>
    public const string TransferQuarantine = "TransferQuarantine";
    /// <summary>Chuyển kho: bao không đạt chất lượng bị loại bỏ (bỏ nguyên bao) ở kho nguồn.</summary>
    public const string TransferDispose = "TransferDispose";
    public const string MillingConsume = "MillingConsume";
    public const string MillingPack = "MillingPack";
    public const string OutboundConsume = "OutboundConsume";
    public const string OutboundStage = "OutboundStage";
    public const string OutboundStageReturn = "OutboundStageReturn";
    public const string DeliveryRestock = "DeliveryRestock";
    public const string CustomerReturn = "CustomerReturn";
    public const string QualityQuarantineSplit = "QualityQuarantineSplit";
    public const string QualityRecheckRelease = "QualityRecheckRelease";
    /// <summary>Trả bao lại nhà cung cấp do bị từ chối kiểm định chất lượng.</summary>
    public const string QualityRejectReturn = "QualityRejectReturn";

    // ── Kiểm kê theo bao ────────────────────────────────────────────────────
    /// <summary>Kiểm kê không tìm thấy bao — bao coi như mất khỏi kho.</summary>
    public const string StockTakeMissing = "StockTakeMissing";
    /// <summary>Kiểm kê cân lại bao và điều chỉnh khối lượng.</summary>
    public const string StockTakeAdjust = "StockTakeAdjust";
    /// <summary>Kiểm kê phát hiện bao thừa, kéo bao về vị trí đang kiểm.</summary>
    public const string StockTakeFound = "StockTakeFound";
    /// <summary>Kiểm kê chuyển bao có vấn đề sang ô cách ly.</summary>
    public const string StockTakeQuarantine = "StockTakeQuarantine";
    /// <summary>Kiểm kê ô cách ly đạt — rút bao về cột thường.</summary>
    public const string StockTakeRelease = "StockTakeRelease";
    /// <summary>Kiểm kê loại bỏ nguyên bao hỏng.</summary>
    public const string StockTakeDispose = "StockTakeDispose";
    /// <summary>Xếp lại thứ tự bao trong cột sau khi lấy ra kiểm kê và cất lại.</summary>
    public const string StockTakeRestow = "StockTakeRestow";
}
