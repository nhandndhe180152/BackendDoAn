using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class Inventory : EntityBase<int>
{
    public int WarehouseId { get; set; }
    public int? LocationId { get; set; }
    public int ProductVariantId { get; set; }
    public int? PurchaseOrderId { get; set; }
    public decimal CostPrice { get; set; }
    public int QuantityOnHand { get; set; }
    public int QuantityReserved { get; set; }
    public int QuantityAvailable => QuantityOnHand - QuantityReserved;
    public DateTime? LastStockTakeDate { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }

    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual ProductVariant ProductVariant { get; set; } = null!;
    public virtual Location? Location { get; set; }
    public virtual PurchaseOrder? PurchaseOrder { get; set; } = null!;
    public virtual ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
}
