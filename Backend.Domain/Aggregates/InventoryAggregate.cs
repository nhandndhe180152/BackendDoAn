using System;

namespace Backend.Domain.Aggregates;

public class InventoryAggregate
{
    public int Id { get; set; }

    public int WarehouseId { get; set; }

    public string WarehouseName { get; set; } = null!;

    public int? LocationId { get; set; }

    public string? LocationCode { get; set; }

    public int ProductVariantId { get; set; }

    public string SKU { get; set; } = null!;

    public string ProductVariantName { get; set; } = null!;

    public int ProductId { get; set; }

    public string ProductName { get; set; } = null!;

    /// <summary>Danh mục sản phẩm (Lúa/Gạo/Tấm/Cám/Trấu) — nguồn cho tab lọc.</summary>
    public int? ProductCategoryId { get; set; }

    public string? CategoryName { get; set; }

    /// <summary>true = phụ phẩm (tấm/cám/trấu); false = lúa hoặc gạo chính.</summary>
    public bool IsByproduct { get; set; }

    /// <summary>Đơn vị tính (UoM) của biến thể — hiển thị kèm số lượng.</summary>
    public string? UnitName { get; set; }

    /// <summary>Khối lượng quy đổi trên 1 đơn vị (kg) — dùng quy đổi tồn ra kg/tấn.</summary>
    public decimal UnitWeightKg { get; set; }

    public decimal CostPrice { get; set; }

    public decimal QuantityOnHand { get; set; }

    public decimal QuantityReserved { get; set; }

    public decimal QuantityAvailable { get; set; }

    /// <summary>Tồn đang cách ly (CL) — lô có trạng thái "Cách ly" hoặc nằm ở vị trí cách ly.</summary>
    public decimal QuantityQuarantine { get; set; }

    public decimal QuarantinedKg { get; set; }

    public decimal SellableOnHandKg { get; set; }

    public decimal OtherBlockedKg { get; set; }

    /// <summary>Tồn đang xử lý (XL) — lô có trạng thái "Chờ xử lý"/"Đang xay".</summary>
    public decimal QuantityProcessing { get; set; }

    /// <summary>Khối lượng tồn thực tế quy đổi (kg) = QuantityOnHand * UnitWeightKg.</summary>
    public decimal TotalWeightKg { get; set; }

    /// <summary>Số bao vật lý đang lưu tại đúng lô/vị trí.</summary>
    public int Bags { get; set; }

    /// <summary>
    /// true khi số bao được đối chiếu từ các bao vật lý PaddyLotBag; false với dữ liệu cũ
    /// chưa được chuyển đổi sang quản lý theo bao.
    /// </summary>
    public bool HasPhysicalBagData { get; set; }

    /// <summary>Số bao vật lý chưa đủ trọng lượng chuẩn.</summary>
    public int OpenBags { get; set; }

    public decimal? MinStockLevel { get; set; }

    public bool IsLowStock { get; set; }

    // ----- Thông tin lô lúa/gạo (PaddyLot) -----

    public int? PaddyLotId { get; set; }

    public string? LotCode { get; set; }

    /// <summary>PADDY (lúa) | RICE (gạo) | BYPRODUCT (tấm/cám/trấu) | PURCHASED_GOOD.</summary>
    public string? LotType { get; set; }

    public DateTime? LotInboundDate { get; set; }

    /// <summary>Mô tả chất lượng lô (vd: "Ẩm 16.2% · chờ xử lý").</summary>
    public string? LotQualityStatus { get; set; }

    public decimal? LotCostPricePerKg { get; set; }

    public int? LotStatusId { get; set; }

    public string? LotStatusName { get; set; }

    public string? LotStatusColor { get; set; }

    /// <summary>Lô có được phép xuất/bán không (false = cách ly / chờ xử lý ...).</summary>
    public bool? LotIsSellable { get; set; }

    public DateTime? LastStockTakeDate { get; set; }

    public DateTime? CreatedDate { get; set; }
}
