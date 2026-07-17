using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class PutawayRuleConfig : EntityAuditBase<int>
{
    public int? WarehouseId { get; set; }

    public decimal CapacityWeight { get; set; } = 0.4000m;
    public decimal OccupancyWeight { get; set; } = 0.3000m;
    public decimal CategoryWeight { get; set; } = 0.2000m;
    public decimal PriorityWeight { get; set; } = 0.1000m;

    public decimal SameProductScore { get; set; } = 1.0000m;
    public decimal EmptyColumnScore { get; set; } = 0.6000m;

    public bool IsActive { get; set; } = true;

    public virtual Warehouse? Warehouse { get; set; }
}
