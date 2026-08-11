namespace Backend.Application.Constants;

public static class PaddyLotBagAllocationStatuses
{
    public const string Active = "ACTIVE";
    public const string Released = "RELEASED";
    public const string Consumed = "CONSUMED";
}

public static class PaddyLotBagAllocationReferenceTypes
{
    public const string MillingOrder = "MILLING_ORDER";
    public const string SalesOrder = "SALES_ORDER";
    public const string OutboundOrder = "OUTBOUND_ORDER";
}
