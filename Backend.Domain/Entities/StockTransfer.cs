using System;
using System.Collections.Generic;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

/// <summary>
/// Phiếu điều chuyển nội bộ giữa các kho/điểm của cùng doanh nghiệp (hỗ trợ đa kho).
/// </summary>
public class StockTransfer : EntityAuditBase<int>
{
    public int? OrganizationId { get; set; }
    public string TransferCode { get; set; } = null!;
    public int StatusId { get; set; }
    public int FromWarehouseId { get; set; }
    public int ToWarehouseId { get; set; }
    public DateTime TransferDate { get; set; }
    public int? AssignedUserId { get; set; }
    public string? Note { get; set; }

    // Navigation
    public virtual Organization? Organization { get; set; }
    public virtual StockTransferStatus Status { get; set; } = null!;
    public virtual Warehouse FromWarehouse { get; set; } = null!;
    public virtual Warehouse ToWarehouse { get; set; } = null!;
    public virtual User? AssignedUser { get; set; }
    public virtual ICollection<StockTransferItem> StockTransferItems { get; set; } = new List<StockTransferItem>();
}
