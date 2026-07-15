using System;

namespace Backend.Domain.Aggregates;

public class QualityInspectionAggregate
{
    public int Id { get; set; }
    public int PaddyLotId { get; set; }
    public string? LotCode { get; set; }
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
    public DateTime CreatedDate { get; set; }
}
