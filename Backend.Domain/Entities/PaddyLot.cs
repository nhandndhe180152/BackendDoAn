using System;
using System.Collections.Generic;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

/// <summary>
/// Lô lúa/gạo — đơn vị truy vết chính.
/// Mỗi lô gắn nguồn mua, giống, chất lượng và giá vốn/kg.
/// Gạo thành phẩm sau xay cũng tạo lô con để truy vết ngược.
/// </summary>
public class PaddyLot : EntityAuditBase<int>
{
    public int? OrganizationId { get; set; }
    public string LotCode { get; set; } = null!;

    /// <summary>PADDY (lúa) | RICE (gạo thành phẩm) | BYPRODUCT (tấm/cám/trấu)</summary>
    public string LotType { get; set; } = null!;

    public int ProductVariantId { get; set; }
    public int? RiceVarietyId { get; set; }
    public int StatusId { get; set; }

    /// <summary>→ PaddyPurchaseReceipt (lô lúa mua vào)</summary>
    public int? SourceReceiptId { get; set; }

    /// <summary>→ MillingOrder (lô gạo/phụ phẩm sinh từ xay)</summary>
    public int? SourceMillingOrderId { get; set; }

    public int WarehouseId { get; set; }
    public int? LocationId { get; set; }
    public DateTime InboundDate { get; set; }
    public decimal InitialWeightKg { get; set; }
    public decimal RemainingWeightKg { get; set; }
    public decimal CostPricePerKg { get; set; }
    public string? QualityStatus { get; set; }
    public string QrCode { get; set; } = string.Empty;
    public string QrImageUrl { get; set; } = string.Empty;
    public int? ParentLotId { get; set; }

    // Navigation
    public virtual Organization? Organization { get; set; }
    public virtual PaddyLot? ParentLot { get; set; }
    public virtual ProductVariant ProductVariant { get; set; } = null!;
    public virtual RiceVariety? RiceVariety { get; set; }
    public virtual LotStatus Status { get; set; } = null!;
    public virtual PaddyPurchaseReceipt? SourceReceipt { get; set; }
    public virtual MillingOrder? SourceMillingOrder { get; set; }
    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual Location? Location { get; set; }
    public virtual ICollection<QualityInspection> QualityInspections { get; set; } = new List<QualityInspection>();
    public virtual ICollection<MillingOrderInput> MillingOrderInputs { get; set; } = new List<MillingOrderInput>();
    public virtual ICollection<PaddyLotBag> Bags { get; set; } = new List<PaddyLotBag>();
    public virtual ICollection<PaddyLotBagContent> BagContents { get; set; } = new List<PaddyLotBagContent>();
}
