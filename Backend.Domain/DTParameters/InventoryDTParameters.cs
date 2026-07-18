using System;
using Backend.Share.Entities;

namespace Backend.Domain.DTParameters;

public class InventoryDTParameters : DTParameter
{
    public int? WarehouseId { get; set; }

    public int? LocationId { get; set; }

    public int? ProductVariantId { get; set; }

    public bool? LowStockOnly { get; set; }

    /// <summary>Lọc theo danh mục sản phẩm (tab Lúa/Gạo/Tấm/Cám/Trấu).</summary>
    public int? ProductCategoryId { get; set; }

    /// <summary>Lọc theo loại lô: PADDY | RICE | BYPRODUCT | PURCHASED_GOOD.</summary>
    public string? LotType { get; set; }

    /// <summary>Lọc theo trạng thái lô (LotStatus.Id).</summary>
    public int? LotStatusId { get; set; }

    /// <summary>true = chỉ lấy tồn còn theo dõi lô (có PaddyLotId).</summary>
    public bool? WithLotOnly { get; set; }
}
