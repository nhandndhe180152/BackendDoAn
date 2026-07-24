using System;

namespace Backend.Application.DTOs.PartyDebts;

public class PartyDebtDetailDto
{
    public int Id { get; set; }
    public int? OrganizationId { get; set; }
    public string PartyType { get; set; } = null!;
    public int PartyId { get; set; }
    public string Direction { get; set; } = null!;
    public decimal OpeningBalance { get; set; }
    public decimal CurrentBalance { get; set; }
    public decimal? CreditLimit { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }

    public decimal DueSoonAmount { get; set; }
    public decimal DueTodayAmount { get; set; }
    public decimal OverdueAmount { get; set; }
    public DateTime? OldestOverdueDate { get; set; }
    public int MaxDaysOverdue { get; set; }
}
