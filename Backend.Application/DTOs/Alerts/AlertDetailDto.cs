using System;

namespace Backend.Application.DTOs.Alerts;

public class AlertDetailDto
{
    public int Id { get; set; }
    public string AlertType { get; set; } = null!;
    public string Severity { get; set; } = null!;
    public int WarehouseId { get; set; }
    public string? WarehouseCode { get; set; }
    public string? WarehouseName { get; set; }
    public int? ProductVariantId { get; set; }
    public string? ProductVariantSku { get; set; }
    public string? ProductName { get; set; }
    public int? LocationId { get; set; }
    public string? LocationName { get; set; }
    public string Message { get; set; } = null!;
    public string? RelatedEntityType { get; set; }
    public int? RelatedEntityId { get; set; }
    public string Status { get; set; } = null!;
    public int? AcknowledgedBy { get; set; }
    public string? AcknowledgedByName { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedDate { get; set; }
}
