using System.Collections.Generic;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

/// <summary>
/// Sổ dư công nợ hai chiều theo từng đối tác:
/// phải trả nông dân (PAYABLE) và phải thu khách hàng (RECEIVABLE).
/// </summary>
public class PartyDebt : EntityAuditBase<int>
{
    public int? OrganizationId { get; set; }

    /// <summary>FARMER | CUSTOMER</summary>
    public string PartyType { get; set; } = null!;

    /// <summary>Id của Farmer hoặc Customer (theo PartyType)</summary>
    public int PartyId { get; set; }

    /// <summary>PAYABLE (phải trả) | RECEIVABLE (phải thu)</summary>
    public string Direction { get; set; } = null!;

    public decimal OpeningBalance { get; set; }
    public decimal CurrentBalance { get; set; }
    public decimal? CreditLimit { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public virtual Organization? Organization { get; set; }
    public virtual ICollection<DebtTransaction> DebtTransactions { get; set; } = new List<DebtTransaction>();
}
