using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

/// <summary>
/// Phân bổ lot/vị trí cho từng dòng OutboundOrderItem trong bước picking.
/// Một OutboundOrderItem có thể được lấy từ nhiều lot/vị trí khác nhau.
/// QuantityAllocated: số lượng dự kiến lấy từ lot này.
/// QuantityPicked: số lượng thực tế đã lấy.
/// </summary>
public class OutboundOrderItemAllocation : EntityAuditBase<int>
{
    public int OutboundOrderItemId { get; set; }

    /// <summary>FK → Inventory (row chứa số lượng tồn theo lot + vị trí)</summary>
    public int InventoryId { get; set; }

    /// <summary>Lô lúa/gạo — null nếu không theo dõi lô</summary>
    public int? PaddyLotId { get; set; }

    public int LocationId { get; set; }

    /// <summary>Số lượng dự kiến lấy từ lot này (tính lúc allocate)</summary>
    public decimal QuantityAllocated { get; set; }

    /// <summary>Số lượng thực tế đã lấy (cập nhật lúc pick)</summary>
    public decimal QuantityPicked { get; set; }

    /// <summary>Giá vốn/kg tại thời điểm xuất kho</summary>
    public decimal UnitCostPrice { get; set; }

    /// <summary>Optimistic concurrency — phát hiện conflict ghi đồng thời</summary>
    public byte[]? RowVersion { get; set; }

    // Navigation
    public virtual OutboundOrderItem OutboundOrderItem { get; set; } = null!;
    public virtual Inventory Inventory { get; set; } = null!;
    public virtual PaddyLot? PaddyLot { get; set; }
    public virtual Location Location { get; set; } = null!;
}
