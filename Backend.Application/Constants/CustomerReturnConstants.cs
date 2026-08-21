namespace Backend.Application.Constants;

public static class CustomerReturnDisposition
{
    public const string PendingInspection = "PENDING_INSPECTION";
    public const string Restock = "RESTOCK";
    public const string Quarantine = "QUARANTINE";
    public const string ReturnToCustomer = "RETURN_TO_CUSTOMER";
    public const string Mixed = "MIXED";
}

public static class CustomerReturnRefundStatus
{
    public const string NotApplicable = "NOT_APPLICABLE";
    public const string Pending = "PENDING";
    public const string PartiallyRefunded = "PARTIALLY_REFUNDED";
    public const string Refunded = "REFUNDED";
    public const string Failed = "FAILED";
}
