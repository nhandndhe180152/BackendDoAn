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
}
