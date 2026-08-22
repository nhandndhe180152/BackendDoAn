using System;

namespace Backend.Application.DTOs.CustomerFeedbacks;

public class CustomerFeedbackDto
{
    public int Id { get; set; }
    public int SalesOrderId { get; set; }
    public string SalesOrderCode { get; set; } = null!;
    
    public int OutboundOrderId { get; set; }
    public string OutboundOrderCode { get; set; } = null!;

    public int? OutboundOrderItemId { get; set; }
    public int? ProductVariantId { get; set; }
    public string? ProductVariantName { get; set; }
    public string? SKU { get; set; }

    public int? PaddyLotBagAllocationId { get; set; }
    public string? OriginalLocationCode { get; set; }

    public string FeedbackType { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string? Severity { get; set; }

    public string ResolutionStatus { get; set; } = null!;
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedByName { get; set; }
    public string? ResolutionNote { get; set; }

    public DateTime CreatedDate { get; set; }
    public string CreatedByName { get; set; } = null!;

    public int? CustomerReturnOrderId { get; set; }
    public string? CustomerReturnOrderCode { get; set; }
}

public class CustomerFeedbackSummaryDto
{
    public int Id { get; set; }
    public int SalesOrderId { get; set; }
    public int OutboundOrderId { get; set; }
    public int? OutboundOrderItemId { get; set; }
    public int? ProductVariantId { get; set; }
    public string? ProductVariantName { get; set; }
    public int? PaddyLotBagAllocationId { get; set; }
    public int? BagId { get; set; }
    public int? BagNo { get; set; }
    public int? PaddyLotId { get; set; }
    public string? PaddyLotCode { get; set; }
    public string FeedbackType { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string? Severity { get; set; }
    public string ResolutionStatus { get; set; } = null!;
    public DateTime CreatedDate { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }
    public int? CustomerReturnOrderId { get; set; }
    public string? CustomerReturnOrderCode { get; set; }
}
