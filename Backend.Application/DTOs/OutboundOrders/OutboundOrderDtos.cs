using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Backend.Application.DTOs.OutboundOrders;

// ═══════════════════════════════ READ ═══════════════════════════════

public class OutboundOrderListDto
{
    public int Id { get; set; }
    public int SalesOrderId { get; set; }
    public string SOCode { get; set; } = null!;
    public string CustomerName { get; set; } = null!;
    public int OutboundStatusId { get; set; }
    public string OutboundStatusName { get; set; } = null!;
    public string OutboundStatusColor { get; set; } = null!;
    public int? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public decimal TotalDispatchedValue { get; set; }
    public decimal TotalDispatchedSaleValue { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string? Note { get; set; }
    public DateTime? CreatedDate { get; set; }
}

public class OutboundOrderDetailDto
{
    public int Id { get; set; }
    public int SalesOrderId { get; set; }
    public string SOCode { get; set; } = null!;
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = null!;
    public int OutboundStatusId { get; set; }
    public string OutboundStatusName { get; set; } = null!;
    public string OutboundStatusColor { get; set; } = null!;
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = null!;
    public decimal TotalDispatchedValue { get; set; }
    public decimal TotalDispatchedSaleValue { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string? Note { get; set; }
    public DateTime? CreatedDate { get; set; }
    public List<OutboundOrderItemDto> Items { get; set; } = new();
}

public class OutboundOrderItemDto
{
    public int Id { get; set; }
    public int ProductVariantId { get; set; }
    public string ProductVariantName { get; set; } = null!;
    public string? SKU { get; set; }
    public decimal QuantityOrdered { get; set; }
    public decimal QuantityPicked { get; set; }
    public decimal UnitCostPrice { get; set; }
    public int? SalesOrderItemId { get; set; }
    public string? Note { get; set; }
    public List<OutboundOrderItemAllocationDto> Allocations { get; set; } = new();
}

public class OutboundOrderItemAllocationDto
{
    public int Id { get; set; }
    public int InventoryId { get; set; }
    public int? PaddyLotId { get; set; }
    public string? PaddyLotCode { get; set; }
    public int LocationId { get; set; }
    public string? LocationCode { get; set; }
    public decimal QuantityAllocated { get; set; }
    public decimal QuantityPicked { get; set; }
    public decimal UnitCostPrice { get; set; }
}

// ═══════════════════════════════ COMMANDS ═══════════════════════════════

/// <summary>
/// Gắn lot/vị trí cho từng dòng OutboundOrderItem (bước ALLOCATE).
/// Chuyển trạng thái OutboundOrder → PICKING.
/// </summary>
public class AllocateOutboundDto
{
    [Required]
    [MinLength(1)]
    public List<AllocateItemDto> Allocations { get; set; } = new();
}

public class AllocateItemDto
{
    [Required]
    public int OutboundOrderItemId { get; set; }

    [Required]
    [MinLength(1)]
    public List<AllocateItemLotDto> Lots { get; set; } = new();
}

public class AllocateItemLotDto
{
    [Required]
    public int InventoryId { get; set; }

    [Range(0.001, double.MaxValue)]
    public decimal QuantityAllocated { get; set; }
}

/// <summary>
/// Cập nhật số lượng thực tế đã lấy cho từng allocation (bước PICK).
/// </summary>
public class PickOutboundDto
{
    [Required]
    [MinLength(1)]
    public List<PickAllocationDto> Picks { get; set; } = new();
}

public class PickAllocationDto
{
    [Required]
    public int AllocationId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal QuantityPicked { get; set; }
}

/// <summary>
/// Xác nhận xuất kho (DISPATCHED). Đây là bước giảm tồn thực sự.
/// </summary>
public class ConfirmDispatchDto
{
    public DateTime? DueDate { get; set; }

    public string? Note { get; set; }
}

// ═══════════════════════════════ PAGED QUERY ═══════════════════════════════

public class OutboundOrderPagedQuery
{
    public string? Keyword { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class CompleteDeliveryDto
{
    [Required(ErrorMessage = "Tên người nhận không được để trống")]
    [MaxLength(255)]
    public string ReceiverName { get; set; } = null!;

    [Range(0, double.MaxValue, ErrorMessage = "Số tiền khách thanh toán thêm không được âm")]
    public decimal PaymentAmount { get; set; }

    [MaxLength(1000)]
    public string? DeliveryNote { get; set; }

    [MaxLength(1000)]
    public string? ProofImageUrl { get; set; }
}

public class FailDeliveryDto
{
    [Required(ErrorMessage = "Lý do thất bại không được để trống")]
    [MaxLength(1000)]
    public string Reason { get; set; } = null!;
}

