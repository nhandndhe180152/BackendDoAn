using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

/// <summary>
/// Cảnh báo tồn kho thấp (FE-03) hoặc nghẽn nhập (FE-12).
/// AlertType: LOW_STOCK | BOTTLENECK
/// </summary>
public class Alert : EntityAuditBase<int>
{
    public string AlertType { get; set; } = null!;       // LOW_STOCK | BOTTLENECK
    public string Severity { get; set; } = null!;        // INFO | WARNING | CRITICAL
    public int WarehouseId { get; set; }
    public int? ProductVariantId { get; set; }
    public int? LocationId { get; set; }
    public string Message { get; set; } = null!;
    public string? RelatedEntityType { get; set; }
    public int? RelatedEntityId { get; set; }
    public string Status { get; set; } = null!;          // OPEN | ACKNOWLEDGED | RESOLVED
    public int? AcknowledgedBy { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? DeduplicationKey { get; set; }

    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual ProductVariant? ProductVariant { get; set; }
    public virtual Location? Location { get; set; }
    public virtual User? AcknowledgedByUser { get; set; }
}
