using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class PaddyLotBagContent : EntityAuditBase<int>
{
    public int BagId { get; set; }
    public int LotId { get; set; }
    public decimal WeightKg { get; set; }

    public virtual PaddyLotBag Bag { get; set; } = null!;
    public virtual PaddyLot Lot { get; set; } = null!;
}
