using System;

namespace Backend.Application.DTOs.MillingYieldConfigs;

/// <summary>
/// DTO tạo mới cấu hình tỷ lệ lúa→gạo (yield) theo giống lúa và dải độ ẩm.
/// </summary>
public class CreateMillingYieldConfigDto
{
    public int? OrganizationId { get; set; }

    /// <summary>null = áp dụng chung cho mọi giống lúa.</summary>
    public int? RiceVarietyId { get; set; }

    public decimal? MoistureFrom { get; set; }
    public decimal? MoistureTo { get; set; }

    /// <summary>Tỷ lệ thu hồi gạo (0..1). Cơ sở tính: kg lúa tiêu hao = kg gạo ÷ YieldRate.</summary>
    public decimal YieldRate { get; set; }

    public decimal? BrokenRiceRate { get; set; }
    public decimal? BranRate { get; set; }
    public decimal? HuskRate { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public bool IsActive { get; set; } = true;

    public int? CreatedBy { get; set; }
}
