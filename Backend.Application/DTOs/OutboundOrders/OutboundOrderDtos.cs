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
    public string OutboundStatusCode { get; set; } = null!;
    public string OutboundStatusColor { get; set; } = null!;
    public int? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public decimal TotalDispatchedValue { get; set; }
    public decimal TotalDispatchedSaleValue { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string? Note { get; set; }
    public string? CancelReason { get; set; }
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
    public string OutboundStatusCode { get; set; } = null!;
    public string OutboundStatusColor { get; set; } = null!;
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = null!;
    public decimal TotalDispatchedValue { get; set; }
    public decimal TotalDispatchedSaleValue { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string? Note { get; set; }
    public string? CancelReason { get; set; }
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
    /// <summary>
    /// Nhóm các allocation cùng lô, vị trí và khối lượng đơn vị để UI không
    /// phải hiển thị hàng trăm dòng bao giống nhau. AllocationIds vẫn được
    /// giữ để client phân bổ ngược khi cập nhật số lượng thực lấy.
    /// </summary>
    public List<OutboundOrderAllocationGroupDto> AllocationGroups { get; set; } = new();
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

public class OutboundOrderAllocationGroupDto
{
    public string GroupKey { get; set; } = null!;
    public List<int> AllocationIds { get; set; } = new();
    public int InventoryId { get; set; }
    public int? PaddyLotId { get; set; }
    public string? PaddyLotCode { get; set; }
    public int LocationId { get; set; }
    public string? LocationCode { get; set; }
    /// <summary>Số dòng bao cùng trọng lượng trong nhóm.</summary>
    public int BagCount { get; set; }
    /// <summary>Khối lượng của mỗi dòng bao trong nhóm.</summary>
    public decimal WeightPerBagKg { get; set; }
    public decimal TotalAllocatedKg { get; set; }
    public decimal TotalPickedKg { get; set; }
}

public class OutboundAllocationCandidateDto
{
    public int InventoryId { get; set; }
    public int ProductVariantId { get; set; }
    public int? PaddyLotId { get; set; }
    public string? LotCode { get; set; }
    public int? LocationId { get; set; }
    public string? LocationCode { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal ReservedByOtherOrders { get; set; }
    public decimal ReservedForThisSalesOrder { get; set; }
    public decimal SelectableQuantity { get; set; }

    // ── Bag-level info cho giao diện Stack Card ──
    /// <summary>Trọng lượng chuẩn 1 bao (vd: 50 kg). Null nếu SKU không có quy cách đóng bao.</summary>
    public decimal? StandardWeightKg { get; set; }
    /// <summary>Số bao nguyên (IsFull=true) khả dụng tại vị trí này.</summary>
    public int FullBagCount { get; set; }
    /// <summary>Có bao lẻ (IsFull=false) ở đỉnh cột hay không.</summary>
    public bool HasOpenBag { get; set; }
    /// <summary>Khối lượng thực tế của bao lẻ (kg).</summary>
    public decimal OpenBagWeightKg { get; set; }
    /// <summary>Id của bao lẻ (PaddyLotBag.Id) – để Frontend có thể tham chiếu nếu cần.</summary>
    public int? OpenBagId { get; set; }
    /// <summary>Bao lẻ có đang bị bao khác chặn ở phía trên (không thể lấy) hay không.</summary>
    public bool IsOpenBagBlocked { get; set; }
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

    /// <summary>Lọc theo trạng thái phiếu xuất (null/0 = tất cả).</summary>
    public int? OutboundStatusId { get; set; }

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

/// <summary>
/// Body của POST /outbound-orders/{id}/cancel — lý do hủy phiếu xuất.
/// Ràng buộc bắt buộc &amp; độ dài nằm ở <c>CancelOutboundOrderDtoValidator</c>
/// (project tắt DataAnnotations, chỉ dùng FluentValidation).
/// </summary>
public class CancelOutboundOrderDto
{
    public string Reason { get; set; } = null!;
}

public class FailDeliveryDto
{
    [Required(ErrorMessage = "Lý do thất bại không được để trống")]
    [MaxLength(1000)]
    public string Reason { get; set; } = null!;
}

