using System;

namespace Backend.Application.DTOs.QualityInspections;

public class UpdateQualityInspectionDto
{
    public int Id { get; set; }
    public int PaddyLotId { get; set; }
    public int? InspectorId { get; set; }
    public DateTime InspectedAt { get; set; }
    public decimal? MoisturePercent { get; set; }
    public decimal? ImpurityPercent { get; set; }
    public string? MoldLevel { get; set; }
    public string? PestLevel { get; set; }
    public string? PackagingStatus { get; set; }
    public bool PassedInspection { get; set; }
    public string? Handling { get; set; }
    public string? Note { get; set; }
    public int? UpdatedBy { get; set; }
}
