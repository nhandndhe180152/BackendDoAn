using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Backend.Application.DTOs.Putaway;

public record GetPutawaySuggestionsRequest(
    [Required] int WarehouseId,
    [Required] int ProductVariantId,
    int? PaddyLotId,
    [Range(0.001, double.MaxValue, ErrorMessage = "Khối lượng cần lưu phải lớn hơn 0.")]
    decimal RequiredWeightKg,
    PutawayPlacementMode PlacementMode = PutawayPlacementMode.Normal,
    int Top = 5);

public enum PutawayPlacementMode
{
    Normal = 1,
    Quarantine = 2
}

public record PutawayScoreDetailsDto(
    decimal CapacityFit,
    decimal OccupancyFit,
    decimal CategoryMatch,
    decimal PriorityNorm);

public record PutawaySuggestionDto(
    int Rank,
    int LocationId,
    string LocationCode,
    string ZoneName,
    decimal CurrentOccupancyKg,
    decimal MaxCapacityKg,
    decimal FreeCapacityKg,
    decimal RemainingAfterKg,
    int? CurrentProductVariantId,
    bool IsEmpty,
    decimal Score,
    PutawayScoreDetailsDto ScoreDetails,
    string Reason);

public record SplitSuggestionDto(
    int LocationId,
    decimal WeightKg,
    string LocationCode,
    string ZoneName,
    decimal FreeCapacityKg);

public record PutawaySuggestionsResponse(
    bool HasSuggestion,
    int WarehouseId,
    int ProductVariantId,
    decimal RequiredWeightKg,
    IReadOnlyList<PutawaySuggestionDto> Suggestions,
    string? Message,
    bool? CanSplit = null,
    decimal? TotalFreeCapacityKg = null,
    IReadOnlyList<SplitSuggestionDto>? SplitSuggestions = null);

public class ConfirmStoreInRequest
{
    [Required]
    public int ProductVariantId { get; set; }

    public int? PaddyLotId { get; set; }

    [Required]
    public int SelectedLocationId { get; set; }

    public int? SuggestedLocationId { get; set; }

    [Range(0.001, double.MaxValue, ErrorMessage = "Khối lượng phải lớn hơn 0.")]
    public decimal WeightKg { get; set; }

    public int? BagCount { get; set; }

    public string? OverrideReason { get; set; }
}

public record ConfirmPaddyStoreInResult(
    int ReceiptId,
    int LotId,
    decimal StoredWeightKg,
    decimal RemainingWeightKg,
    bool IsFullyStored);

public class PutawayRuleConfigDto
{
    public int Id { get; set; }
    public int? WarehouseId { get; set; }
    public decimal CapacityWeight { get; set; }
    public decimal OccupancyWeight { get; set; }
    public decimal CategoryWeight { get; set; }
    public decimal PriorityWeight { get; set; }
    public decimal SameProductScore { get; set; }
    public decimal EmptyColumnScore { get; set; }
    public bool IsActive { get; set; }
}

public class UpdatePutawayRuleConfigDto
{
    [Required]
    [Range(0.0, 1.0)]
    public decimal CapacityWeight { get; set; }

    [Required]
    [Range(0.0, 1.0)]
    public decimal OccupancyWeight { get; set; }

    [Required]
    [Range(0.0, 1.0)]
    public decimal CategoryWeight { get; set; }

    [Required]
    [Range(0.0, 1.0)]
    public decimal PriorityWeight { get; set; }

    [Required]
    [Range(0.0, 100.0)]
    public decimal SameProductScore { get; set; }

    [Required]
    [Range(0.0, 100.0)]
    public decimal EmptyColumnScore { get; set; }
}
