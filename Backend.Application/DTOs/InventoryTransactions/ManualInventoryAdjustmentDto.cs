using System;

namespace Backend.Application.DTOs.InventoryTransactions;

public class ManualInventoryAdjustmentDto
{
    public int ProductVariantId { get; set; }

    public int WarehouseId { get; set; }

    public int? LocationId { get; set; }

    /// ID lô hàng (PaddyLotId). Bắt buộc điền khi điều chỉnh hàng tồn có lô (ví dụ: lúa gạo),
    /// nếu không, điều chỉnh sẽ tạo/sửa nhầm dòng tồn null-lot song song với dòng theo lô.
    public int? PaddyLotId { get; set; }

    public decimal? NewQuantityOnHand { get; set; }

    public decimal? AdjustmentQuantity { get; set; }

    public string Reason { get; set; } = null!;
}
