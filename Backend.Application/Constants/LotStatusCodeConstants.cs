namespace Backend.Application.Constants;

public static class LotStatusCodeConstants
{
    public const string PendingInbound = "PENDING_INBOUND";
    public const string InStock        = "IN_STOCK";
    public const string Processing     = "PROCESSING";
    public const string Quarantine     = "QUARANTINE";
    public const string Milling        = "MILLING";
    public const string Depleted       = "DEPLETED";
}
