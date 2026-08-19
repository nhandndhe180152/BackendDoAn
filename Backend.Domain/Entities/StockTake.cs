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
    public int? ApprovedByUserId { get; set; }
    public string? ApproveNote { get; set; }

    // ── Phạm vi kiểm kê (lưu DB để biết phiếu chụp theo khu/cột/lô nào) ───────
    /// <summary>WAREHOUSE | ZONE | COLUMN | LOT.</summary>
    public string? ScopeType { get; set; }
    public string? ScopeZoneName { get; set; }
    public int? ScopeLocationId { get; set; }
    public int? ScopePaddyLotId { get; set; }

    /// <summary>
    /// Phiếu kiểm kê KHU CÁCH LY: mọi vị trí trong phạm vi đều là ô cách ly.
    /// Phiếu loại này cho phép rút bao đạt ra và cất về cột thường (Disposition = RELEASE).
    /// </summary>
    public bool IsQuarantineScope { get; set; }

    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual StockTakeStatus StockTakeStatus { get; set; } = null!;
    public virtual User? ApprovedByUser { get; set; }
    public virtual Location? ScopeLocation { get; set; }
    public virtual PaddyLot? ScopePaddyLot { get; set; }
    public virtual ICollection<StockTakeItem> StockTakeItems { get; set; } = new List<StockTakeItem>();
}
