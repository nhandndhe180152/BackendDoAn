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
    /// <summary>Khóa duy nhất variant:warehouse, chỉ có giá trị khi là bao Finished đang mở.</summary>
    public string? OpenBagKey { get; set; }
    public int? SourceBagId { get; set; }

    /// <summary>W14-J: Mã thiết bị cân đã sử dụng khi cân bao này (ví dụ: "SCALE-01").</summary>
    public string? ScaleDeviceRef { get; set; }
    /// <summary>W14-J: Phương thức cân — "SCALE" (cân điện tử BLE) hoặc "MANUAL" (nhập tay).</summary>
    public string? WeightCaptureMethod { get; set; }
    /// <summary>W14-J: Thời điểm thực hiện cân.</summary>
    public DateTime? WeighedAt { get; set; }
    /// <summary>W14-J: ID người thực hiện cân (FK User, nullable SET NULL).</summary>
    public int? WeighedBy { get; set; }

    public virtual PaddyLot Lot { get; set; } = null!;
    public virtual Location? Location { get; set; }
    public virtual ICollection<PaddyLotBagContent> Contents { get; set; } = new List<PaddyLotBagContent>();
    public virtual ICollection<PaddyLotBagMovement> Movements { get; set; } = new List<PaddyLotBagMovement>();
    public virtual ICollection<PaddyLotBagAllocation> Allocations { get; set; } = new List<PaddyLotBagAllocation>();
    public virtual ICollection<QualityInspectionBagResult> QualityResults { get; set; }
        = new List<QualityInspectionBagResult>();
    public virtual PaddyLotBag? SourceBag { get; set; }
    public virtual ICollection<PaddyLotBag> SplitBags { get; set; } = new List<PaddyLotBag>();
}
