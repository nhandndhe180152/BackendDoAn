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
    public int QuantityOnHand { get; set; }
    public int QuantityReserved { get; set; }
    public int QuantityAvailable => QuantityOnHand - QuantityReserved;

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
    public virtual ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
}
