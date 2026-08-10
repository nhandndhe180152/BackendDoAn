using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class PaddyLotBag : EntityAuditBase<int>
{
    public int LotId { get; set; }
    public int BagNo { get; set; }
    public decimal WeightKg { get; set; }
    public int? LocationId { get; set; }
    public string Status { get; set; } = "Pending";
    public string? QrCode { get; set; }
    public int StackOrder { get; set; }
    public decimal? StandardWeightKg { get; set; }
    public bool IsFull { get; set; } = true;
    public string BagKind { get; set; } = "Purchase";
    /// <summary>Khóa duy nhất variant:warehouse:location, chỉ có giá trị khi là bao Finished đang mở.</summary>
    public string? OpenBagKey { get; set; }

    public virtual PaddyLot Lot { get; set; } = null!;
    public virtual Location? Location { get; set; }
    public virtual ICollection<PaddyLotBagContent> Contents { get; set; } = new List<PaddyLotBagContent>();
    public virtual ICollection<PaddyLotBagMovement> Movements { get; set; } = new List<PaddyLotBagMovement>();
}
