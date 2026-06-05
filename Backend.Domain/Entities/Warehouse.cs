using System;
using System.Collections.Generic;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class Warehouse : EntityCommonBase<int>
{
    public string Code { get; set; } = null!;
    public string? Address { get; set; }
    public bool IsActive { get; set; }

    public virtual ICollection<Location> Locations { get; set; } = new List<Location>();
    public virtual ICollection<Inventory> Inventories { get; set; } = new List<Inventory>();
    public virtual ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
    public virtual ICollection<IotDevice> IotDevices { get; set; } = new List<IotDevice>();
    public virtual ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
    public virtual ICollection<SalesOrder> SalesOrders { get; set; } = new List<SalesOrder>();
    public virtual ICollection<StockTake> StockTakes { get; set; } = new List<StockTake>();
    public virtual ICollection<StockAlertConfig> StockAlertConfigs { get; set; } = new List<StockAlertConfig>();
}
