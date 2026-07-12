using System;
using System.Collections.Generic;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class Inventory : EntityBase<int>
{
    public int WarehouseId { get; set; }
    public int? LocationId { get; set; }
    public int ProductVariantId { get; set; }
    public decimal CostPrice { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal QuantityReserved { get; set; }
    public decimal QuantityAvailable => QuantityOnHand - QuantityReserved;

    /// <summary>
    /// Truy vết tồn theo lô lúa/gạo (PaddyLot).
    /// null = hàng không theo dõi lô.
    /// </summary>
    public int? PaddyLotId { get; set; }

    /// <summary>
    /// Optimistic concurrency token — tự động tăng khi EF Core cập nhật dòng này.
    /// Dùng để phát hiện xung đột ghi đồng thời (DbUpdateConcurrencyException).
    /// </summary>
    public byte[]? RowVersion { get; set; }

    public DateTime? LastStockTakeDate { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }

    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual ProductVariant ProductVariant { get; set; } = null!;
    public virtual Location? Location { get; set; }
    public virtual PaddyLot? PaddyLot { get; set; }
    public virtual ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
}

