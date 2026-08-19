using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class PaddyLotBagAllocation : EntityAuditBase<int>
{
    public int BagId { get; set; }
    public string ReferenceType { get; set; } = null!;
    public int ReferenceId { get; set; }
    public int? ReferenceItemId { get; set; }
    public decimal AllocatedWeightKg { get; set; }
    public decimal ConsumedWeightKg { get; set; }
    public string Status { get; set; } = null!;
    public decimal BagWeightSnapshotKg { get; set; }
    public int StackOrderSnapshot { get; set; }

    // ── W14-H: Pick tracking (Status vẫn ACTIVE khi picking — không mất unique ActiveBagId lock) ──
    /// <summary>Kg thực tế đã pick từ bag này. 0 = chưa pick, > 0 = đã pick.</summary>
    public decimal PickedWeightKg { get; set; } = 0m;

    /// <summary>Thời điểm picker xác nhận đã lấy bao này.</summary>
    public DateTime? PickedAt { get; set; }

    /// <summary>UserId người thực hiện pick.</summary>
    public int? PickedBy { get; set; }

    public virtual PaddyLotBag Bag { get; set; } = null!;
}

