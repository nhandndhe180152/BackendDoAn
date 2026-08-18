using System;
using System.Collections.Generic;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class Location : EntityAuditBase<int>
{
    public int WarehouseId { get; set; }
    public string ZoneName { get; set; } = null!;
    public string? ShelfRow { get; set; }
    public string? ShelfLevel { get; set; }
    public string? SlotCode { get; set; }
    public decimal? MaxCapacity { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }

    // FE-13 / FE-16
    public decimal CurrentOccupancy { get; set; }
    public int? AllowedCategoryId { get; set; }
    public int Priority { get; set; }
    public bool IsQuarantine { get; set; }

    /// <summary>Vị trí trung chuyển dành riêng cho hàng đã đóng gói và đang chờ xuất.</summary>
    public bool IsOutboundStaging { get; set; }

    /// <summary>
    /// Phiếu xuất đang khóa cột trong lúc nhân viên lấy hàng. Null nghĩa là cột có thể nhận hàng mới.
    /// </summary>
    public int? OutboundLockOrderId { get; set; }
    public DateTime? OutboundLockedAt { get; set; }

    /// <summary>Phát hiện hai phiếu cùng cố khóa một cột.</summary>
    public int? CurrentProductVariantId { get; set; }
    public bool IsSingleTypeColumn { get; set; } = true;
    public string QrCode { get; set; } = string.Empty;
    public string QrImageUrl { get; set; } = string.Empty;

    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual ProductCategory? AllowedCategory { get; set; }
    public virtual ProductVariant? CurrentProductVariant { get; set; }
    public virtual OutboundOrder? OutboundLockOrder { get; set; }
}
