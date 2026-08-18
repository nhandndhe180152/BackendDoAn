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

    public virtual PaddyLotBag Bag { get; set; } = null!;
}
