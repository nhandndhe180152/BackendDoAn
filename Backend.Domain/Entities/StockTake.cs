using System;
using System.Collections.Generic;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class StockTake : EntityAuditBase<int>
{
    public int WarehouseId { get; set; }
    public int StockTakeStatusId { get; set; }
    public string STCode { get; set; } = null!;
    public string? Note { get; set; }
    public DateTime? StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public int? ApprovedBy { get; set; }

    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual StockTakeStatus StockTakeStatus { get; set; } = null!;
    public virtual User? ApprovedByUser { get; set; }
    public virtual ICollection<StockTakeItem> StockTakeItems { get; set; } = new List<StockTakeItem>();
}
