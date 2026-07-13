using System;
using System.Collections.Generic;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class StockTakeStatus : EntityAuditBase<int>
{
    /// <summary>
    /// Mã định danh ổn định (bất biến) dùng trong code thay cho Id số của DB.
    /// Vd: Draft/Submitted/Approved/Rejected.
    /// </summary>
    public string? Code { get; set; }

    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;

    public virtual ICollection<StockTake> StockTakes { get; set; } = new List<StockTake>();
}
