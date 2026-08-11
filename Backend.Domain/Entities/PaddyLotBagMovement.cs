using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class PaddyLotBagMovement : EntityAuditBase<int>
{
    public int BagId { get; set; }
    public string MovementType { get; set; } = string.Empty;
    public int? FromLocationId { get; set; }
    public int? ToLocationId { get; set; }
    public decimal WeightKg { get; set; }
    public decimal BeforeWeightKg { get; set; }
    public decimal AfterWeightKg { get; set; }
    public string ReferenceType { get; set; } = string.Empty;
    public int ReferenceId { get; set; }
    public int? ReferenceItemId { get; set; }
    public string? Note { get; set; }

    public virtual PaddyLotBag Bag { get; set; } = null!;
    public virtual Location? FromLocation { get; set; }
    public virtual Location? ToLocation { get; set; }
}
