using System.Collections.Generic;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

/// <summary>
/// Danh mục nông dân / người bán lúa.
/// Cấu trúc đối xứng với Supplier nhưng phục vụ đầu vào thu mua và công nợ phải trả.
/// </summary>
public class Farmer : EntityAuditBase<int>
{
    public int? OrganizationId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Region { get; set; }
    public string? ReputationNote { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public virtual Organization? Organization { get; set; }
    public virtual ICollection<PaddyPurchaseSchedule> PaddyPurchaseSchedules { get; set; } = new List<PaddyPurchaseSchedule>();
    public virtual ICollection<PaddyPurchaseReceipt> PaddyPurchaseReceipts { get; set; } = new List<PaddyPurchaseReceipt>();
}
