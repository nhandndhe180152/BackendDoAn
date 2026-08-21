using System;
using System.Collections.Generic;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

/// <summary>
/// Đơn hoàn hàng từ khách hàng (FE-16)
/// </summary>
public class CustomerReturnOrder : EntityAuditBase<int>
{
    public int? OrganizationId { get; set; }
    public int WarehouseId { get; set; }
    public int CustomerReturnOrderStatusId { get; set; }
    public int? OutboundOrderId { get; set; }
    public string ReturnCode { get; set; } = null!;
    public string? ReturnReason { get; set; }
    public string? Note { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public int? ApprovedBy { get; set; }
    public DateTime? CompletedDate { get; set; }

    // Financial calculations
    public decimal ApprovedCreditAmount { get; set; }
    public decimal DebtReductionAmount { get; set; }
    public decimal RefundPendingAmount { get; set; }

    public DateTime? ConfirmedAt { get; set; }
    public int? ConfirmedByUserId { get; set; }

    public int? CustomerFeedbackId { get; set; }

    /// <summary>Khách hàng trả hàng — FK → Customer</summary>
    public int? CustomerId { get; set; }

    public virtual Organization? Organization { get; set; }
    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual CustomerReturnOrderStatus CustomerReturnOrderStatus { get; set; } = null!;
    public virtual OutboundOrder? OutboundOrder { get; set; }
    public virtual CustomerFeedback? CustomerFeedback { get; set; }
    public virtual User? ApprovedByUser { get; set; }
    public virtual User? ConfirmedByUser { get; set; }
    public virtual Customer? Customer { get; set; }
    public virtual ICollection<CustomerReturnOrderItem> Items { get; set; } = new List<CustomerReturnOrderItem>();
}
