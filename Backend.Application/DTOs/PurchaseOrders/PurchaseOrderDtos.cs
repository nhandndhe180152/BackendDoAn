using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Backend.Application.DTOs.PurchaseOrders;

// ═══════════════════════════════ CREATE ═══════════════════════════════

public class CreatePurchaseOrderDto
{
    [Required]
    public int SupplierId { get; set; }

    [Required]
    public int WarehouseId { get; set; }

    public DateTime? ExpectedDate { get; set; }

    public string? Note { get; set; }

    public int? OrganizationId { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "Đơn mua phải có ít nhất 1 dòng sản phẩm.")]
    public List<CreatePurchaseOrderItemDto> Items { get; set; } = new();

    public int? CreatedBy { get; set; }
}

public class CreatePurchaseOrderItemDto
{
    [Required]
    public int ProductVariantId { get; set; }

    [Range(0.001, double.MaxValue, ErrorMessage = "Số lượng đặt phải lớn hơn 0.")]
    public decimal QuantityOrdered { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Đơn giá không được âm.")]
    public decimal UnitCostPrice { get; set; }

    public string? Note { get; set; }
}

// ═══════════════════════════════ UPDATE ═══════════════════════════════

public class UpdatePurchaseOrderDto
{
    public int Id { get; set; }
    public DateTime? ExpectedDate { get; set; }
    public string? Note { get; set; }
    public List<UpdatePurchaseOrderItemDto> Items { get; set; } = new();
    public int? UpdatedBy { get; set; }
}

public class UpdatePurchaseOrderItemDto
{
    public int? Id { get; set; }

    [Required]
    public int ProductVariantId { get; set; }

    [Range(0.001, double.MaxValue)]
    public decimal QuantityOrdered { get; set; }

    [Range(0, double.MaxValue)]
    public decimal UnitCostPrice { get; set; }

    public string? Note { get; set; }
}

// ═══════════════════════════════ READ ═══════════════════════════════

public class PurchaseOrderListDto
{
    public int Id { get; set; }
    public string POCode { get; set; } = null!;
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = null!;
    public int StatusId { get; set; }
    public string StatusName { get; set; } = null!;
    public string StatusCode { get; set; } = null!;
    public string StatusColor { get; set; } = null!;
    public int? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Note { get; set; }
    public DateTime? CreatedDate { get; set; }
}

public class PurchaseOrderDetailDto
{
    public int Id { get; set; }
    public string POCode { get; set; } = null!;
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = null!;
    public string? SupplierPhone { get; set; }
    public int StatusId { get; set; }
    public string StatusName { get; set; } = null!;
    public string StatusCode { get; set; } = null!;
    public string StatusColor { get; set; } = null!;
    public int? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Note { get; set; }
    public DateTime? CreatedDate { get; set; }
    public List<PurchaseOrderItemDto> Items { get; set; } = new();
    public List<PurchaseOrderInboundSummaryDto> InboundOrders { get; set; } = new();
}

public class PurchaseOrderItemDto
{
    public int Id { get; set; }
    public int ProductVariantId { get; set; }
    public string ProductVariantName { get; set; } = null!;
    public string? SKU { get; set; }
    public decimal QuantityOrdered { get; set; }
    public decimal QuantityReceived { get; set; }
    public decimal QuantityRemaining { get; set; }
    public decimal UnitCostPrice { get; set; }
    public decimal LineAmount { get; set; }
    public string? Note { get; set; }
}

public class PurchaseOrderInboundSummaryDto
{
    public int Id { get; set; }
    public string InboundStatusName { get; set; } = null!;
    public string InboundStatusCode { get; set; } = null!;
    public DateTime? CompletedDate { get; set; }
}

// ═══════════════════════════════ PAGED QUERY ═══════════════════════════════

public class PurchaseOrderPagedQuery
{
    public string? Keyword { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
