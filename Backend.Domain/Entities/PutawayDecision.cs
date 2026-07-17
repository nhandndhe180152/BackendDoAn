using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class PutawayDecision : EntityAuditBase<long>
{
    public int WarehouseId { get; set; }
    public int ProductVariantId { get; set; }
    public int? PaddyLotId { get; set; }

    public string ReferenceType { get; set; } = null!;
    public int ReferenceId { get; set; }

    public decimal RequiredWeightKg { get; set; }

    public int? SuggestedLocationId { get; set; }
    public int SelectedLocationId { get; set; }

    public decimal? SuggestedScore { get; set; }
    public bool IsOverride { get; set; }
    public string? OverrideReason { get; set; }

    public string? ScoreDetailsJson { get; set; }

    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual ProductVariant ProductVariant { get; set; } = null!;
    public virtual PaddyLot? PaddyLot { get; set; }
    public virtual Location? SuggestedLocation { get; set; }
    public virtual Location SelectedLocation { get; set; } = null!;
}
