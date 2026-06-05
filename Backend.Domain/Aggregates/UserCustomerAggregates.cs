using System;

namespace Backend.Domain.Aggregates;

public class UserCustomerAggregates
{
    public int RequesterId { get; set; }
    public string FullName { get; set; } = null!;
    public string UserStatusColor { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public int RequestStatusId { get; set; }
    public string RequestStatusName { get; set; } = null!;
    public int TotalRequest { get; set; }
    public decimal TotalFees { get; set; }
    public decimal TotalRemuneration { get; set; }
    public DateTime CreatedDate { get; set; }
}
