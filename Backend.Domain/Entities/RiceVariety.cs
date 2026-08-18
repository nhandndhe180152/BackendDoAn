using System.Collections.Generic;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

/// <summary>
/// Danh mục giống lúa/mùa vụ.
/// Là khoá để cấu hình tỷ lệ chuyển đổi (yield) và đánh giá chất lượng.
/// </summary>
public class RiceVariety : EntityAuditBase<int>
{
    public int? OrganizationId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Season { get; set; }
    public decimal? DefaultYieldRate { get; set; }
    public string? Note { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public virtual Organization? Organization { get; set; }
    public virtual ICollection<MillingYieldConfig> MillingYieldConfigs { get; set; } = new List<MillingYieldConfig>();
    public virtual ICollection<PaddyLot> PaddyLots { get; set; } = new List<PaddyLot>();
}
