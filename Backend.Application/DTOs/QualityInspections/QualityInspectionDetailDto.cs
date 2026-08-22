using System;

namespace Backend.Application.DTOs.QualityInspections;

public class QualityInspectionDetailDto
{
    public int Id { get; set; }
    public int PaddyLotId { get; set; }
    public string? LotCode { get; set; }
    /// <summary>Code trạng thái lô (AWAITING_QC = phiếu kiểm định nháp, chờ nhập kết quả).</summary>
    public string? LotStatusCode { get; set; }
    public string? InspectionType { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? CompletedBy { get; set; }
    public int? InspectorId { get; set; }
    public string? InspectorName { get; set; }
    public DateTime InspectedAt { get; set; }
    public decimal? MoisturePercent { get; set; }
    public decimal? ImpurityPercent { get; set; }
    public string? MoldLevel { get; set; }
    public string? PestLevel { get; set; }
    public string? PackagingStatus { get; set; }
    public bool PassedInspection { get; set; }
    public string? Handling { get; set; }
    public string? Note { get; set; }
    public decimal? AffectedWeightKg { get; set; }
    public int? TargetedBagCount { get; set; }
    public decimal? TargetedWeightKg { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }
}
