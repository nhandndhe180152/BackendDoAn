using System;

namespace Backend.Application.DTOs.QualityInspections;

/// <summary>
/// Kết quả kiểm tra của 1 bao trong một inspection session (item trong GET /bags).
/// </summary>
public class QualityInspectionBagResultDto
{
    public int BagId { get; set; }
    public int BagNo { get; set; }
    public decimal WeightKg { get; set; }
    public string Status { get; set; } = null!;
    public int? LocationId { get; set; }
    public string? LocationCode { get; set; }

    // ── Kết quả QC (null nếu chưa kiểm) ─────────────────────────────────────
    public string? QualityResult { get; set; }
    public string? Disposition { get; set; }
    public decimal? MoisturePercent { get; set; }
    public decimal? ImpurityPercent { get; set; }
    public string? MoldLevel { get; set; }
    public string? PestLevel { get; set; }
    public string? PackagingStatus { get; set; }
    public string? Handling { get; set; }
    public string? Note { get; set; }
    public DateTime? InspectedAt { get; set; }
    public string? InspectorName { get; set; }
}
