using System;
using Backend.Domain.Abstractions;
using Backend.Domain.Enums;

namespace Backend.Domain.Entities;

public class CustomerFeedback : EntityAuditBase<int>
{
    public int SalesOrderId { get; set; }
    public int OutboundOrderId { get; set; }
    
    public int? OutboundOrderItemId { get; set; }
    public int? ProductVariantId { get; set; }
    
    /// <summary>
    /// ID của Bag Allocation. Server sẽ kiểm tra xem Allocation này có thuộc về OutboundOrder không.
    /// Nếu có giá trị, dùng để trace về lô lúa/quy trình xay xát.
    /// </summary>
    public int? PaddyLotBagAllocationId { get; set; }

    /// <summary>
    /// Loại feedback (QUALITY, WRONG_PRODUCT, WEIGHT, PACKAGING, DELIVERY, OTHER)
    /// </summary>
    public string FeedbackType { get; set; } = null!;

    public string Description { get; set; } = null!;

    /// <summary>
    /// Mức độ nghiêm trọng của khiếu nại
    /// </summary>
    public string? Severity { get; set; }

    /// <summary>
    /// Trạng thái giải quyết (OPEN, INVESTIGATING, RESOLVED, REJECTED)
    /// </summary>
    public string ResolutionStatus { get; set; } = CustomerFeedbackStatus.Open;

    public DateTime? ResolvedAt { get; set; }
    public int? ResolvedBy { get; set; }
    public string? ResolutionNote { get; set; }

    // Navigation properties
    public virtual SalesOrder SalesOrder { get; set; } = null!;
    public virtual OutboundOrder OutboundOrder { get; set; } = null!;
    public virtual OutboundOrderItem? OutboundOrderItem { get; set; }
    public virtual ProductVariant? ProductVariant { get; set; }
    public virtual PaddyLotBagAllocation? PaddyLotBagAllocation { get; set; }
    public virtual User? ResolvedByUser { get; set; }
    public virtual CustomerReturnOrder? CustomerReturnOrder { get; set; }
}
