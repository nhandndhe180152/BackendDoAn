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
}
