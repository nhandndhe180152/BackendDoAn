using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Backend.Application.DTOs.CustomerFeedbacks;

namespace Backend.Application.DTOs.SalesOrders;

// ═══════════════════════════════ CREATE ═══════════════════════════════

public class CreateSalesOrderDto
{
    [Required]
    public int CustomerId { get; set; }

    [Required]
    public int WarehouseId { get; set; }

    /// <summary>DIRECT | WHOLESALE</summary>
    [Required]
    [RegularExpression("^(DIRECT|WHOLESALE)$", ErrorMessage = "Channel phải là DIRECT hoặc WHOLESALE.")]
    public string Channel { get; set; } = null!;

    public DateTime? ExpectedDeliveryDate { get; set; }

    public bool RequiresMilling { get; set; } = false;

    [Range(0, double.MaxValue)]
    public decimal? DepositAmount { get; set; }

    public string? ShippingAddress { get; set; }
    public string? Note { get; set; }
    public int? OrganizationId { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "Đơn bán phải có ít nhất 1 dòng sản phẩm.")]
    public List<CreateSalesOrderItemDto> Items { get; set; } = new();

    // Set by controller
    public int? CreatedBy { get; set; }
}

public class CreateSalesOrderItemDto
{
    [Required]
    public int ProductVariantId { get; set; }

    [Range(0.001, double.MaxValue, ErrorMessage = "Số lượng phải lớn hơn 0.")]
    public decimal QuantityOrdered { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Đơn giá không được âm.")]
    public decimal UnitSalePrice { get; set; }

    [Range(0, double.MaxValue)]
    public decimal DiscountAmount { get; set; } = 0;

    public string? Note { get; set; }
}

// ═══════════════════════════════ UPDATE ═══════════════════════════════

public class UpdateSalesOrderDto
{
    public int Id { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string? ShippingAddress { get; set; }
    public decimal? DepositAmount { get; set; }
    public string? Note { get; set; }
    public List<UpdateSalesOrderItemDto> Items { get; set; } = new();
    public int? UpdatedBy { get; set; }
}

public class UpdateSalesOrderItemDto
{
    public int? Id { get; set; }

    [Required]
    public int ProductVariantId { get; set; }

    [Range(0.001, double.MaxValue)]
    public decimal QuantityOrdered { get; set; }

    [Range(0, double.MaxValue)]
    public decimal UnitSalePrice { get; set; }

    [Range(0, double.MaxValue)]
    public decimal DiscountAmount { get; set; } = 0;

    public string? Note { get; set; }
}

// ═══════════════════════════════ CANCEL ═══════════════════════════════

/// <summary>
/// Body của POST /sales-orders/{id}/cancel — lý do hủy đơn.
/// Ràng buộc bắt buộc &amp; độ dài nằm ở <c>CancelSalesOrderDtoValidator</c>
/// (project tắt DataAnnotations, chỉ dùng FluentValidation).
/// </summary>
public class CancelSalesOrderDto
{
    public string Reason { get; set; } = null!;
}

// ═══════════════════════════════ READ ═══════════════════════════════

public class SalesOrderListDto
{
    public int Id { get; set; }
    public string SOCode { get; set; } = null!;
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = null!;
    public int StatusId { get; set; }
    public string StatusName { get; set; } = null!;
    public string StatusCode { get; set; } = null!;
    public string StatusColor { get; set; } = null!;
    public string Channel { get; set; } = null!;
    public int? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public bool RequiresMilling { get; set; }
    public int? RiceVarietyId { get; set; }
    public string? RiceVarietyCode { get; set; }
    public string? RiceVarietyName { get; set; }
    public string? RiceVarietyDisplayName { get; set; }
    public int RiceVarietyCount { get; set; }
    public bool HasUnconfiguredRiceVariety { get; set; }
    public decimal TotalRiceRequiredKg { get; set; }
    public decimal AllocatedMillingRiceKg { get; set; }
    public decimal RemainingMillingRiceKg { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal? DepositAmount { get; set; }
    public string? Note { get; set; }
    public string? CancelReason { get; set; }
    public DateTime? CreatedDate { get; set; }
}

public class SalesOrderDetailDto
{
    public int Id { get; set; }
    public string SOCode { get; set; } = null!;
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = null!;
    public string? CustomerPhone { get; set; }
    public int StatusId { get; set; }
    public string StatusName { get; set; } = null!;
    public string StatusCode { get; set; } = null!;
    public string StatusColor { get; set; } = null!;
    public string Channel { get; set; } = null!;
    public int? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public bool RequiresMilling { get; set; }

    /// <summary>
    /// Số lệnh xay còn hiệu lực (bỏ lệnh đã xóa và đã HỦY) đang gắn với đơn.
    /// Client dùng để ẩn lối tắt "Xay xát" khi đơn đã có lệnh xay liên quan.
    /// </summary>
    public int MillingOrderCount { get; set; }

    /// <summary>Tổng khối lượng gạo (kg) đơn yêu cầu, bỏ qua dòng phụ phẩm.</summary>
    public decimal TotalRiceRequiredKg { get; set; }

    /// <summary>Khối lượng gạo (kg) đã được các lệnh xay còn hiệu lực nhận.</summary>
    public decimal AllocatedMillingRiceKg { get; set; }

    /// <summary>Khối lượng gạo (kg) còn phải xay = tổng yêu cầu - đã nhận.</summary>
    public decimal RemainingMillingRiceKg { get; set; }

    public decimal TotalAmount { get; set; }
    public decimal? DepositAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public string? ShippingAddress { get; set; }
    public string? Note { get; set; }
    public string? CancelReason { get; set; }
    public DateTime? CreatedDate { get; set; }
    public int OutboundCount { get; set; }
    public int FeedbackCount { get; set; }
    public List<SalesOrderItemDto> Items { get; set; } = new();
    public List<SalesOrderOutboundSummaryDto> OutboundOrders { get; set; } = new();
    public List<CustomerFeedbackSummaryDto> Feedbacks { get; set; } = new();
}

public class SalesOrderItemDto
{
    public int Id { get; set; }
    public int ProductVariantId { get; set; }
    public string ProductVariantName { get; set; } = null!;
    public string? SKU { get; set; }
    public decimal QuantityOrdered { get; set; }
    public decimal UnitSalePrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineAmount { get; set; }
    public string? Note { get; set; }
}

public class SalesOrderOutboundSummaryDto
{
    public int Id { get; set; }
    public int OutboundStatusId { get; set; }
    public string OutboundStatusName { get; set; } = null!;
    public string OutboundStatusCode { get; set; } = null!;
    public int? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public decimal TotalDispatchedValue { get; set; }
    public decimal TotalDispatchedSaleValue { get; set; }
    public DateTime? CompletedDate { get; set; }
    public int FeedbackCount { get; set; }
}

// ═══════════════════════════════ PAGED QUERY ═══════════════════════════════

public class SalesOrderPagedQuery
{
    public string? Keyword { get; set; }

    /// <summary>Lọc theo trạng thái đơn (null/0 = tất cả).</summary>
    public int? StatusId { get; set; }

    /// <summary>Lọc theo kênh bán: DIRECT | WHOLESALE (null/rỗng = tất cả).</summary>
    public string? Channel { get; set; }

    public int? CustomerId { get; set; }
    public int? WarehouseId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
