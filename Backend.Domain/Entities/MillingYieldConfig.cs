using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

/// <summary>
/// Tỷ lệ lúa→gạo (yield) theo giống lúa và dải độ ẩm.
/// Cơ sở tính tồn: kg lúa tiêu hao = kg gạo ÷ YieldRate.
/// </summary>
public class MillingYieldConfig : EntityAuditBase<int>
{
    public int? OrganizationId { get; set; }

    /// <summary>null = áp dụng chung mọi giống</summary>
    public int? RiceVarietyId { get; set; }

    public decimal? MoistureFrom { get; set; }
    public decimal? MoistureTo { get; set; }
    public decimal YieldRate { get; set; }
    public decimal? BrokenRiceRate { get; set; }
    public decimal? BranRate { get; set; }
    public decimal? HuskRate { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public virtual Organization? Organization { get; set; }
    public virtual RiceVariety? RiceVariety { get; set; }
}
