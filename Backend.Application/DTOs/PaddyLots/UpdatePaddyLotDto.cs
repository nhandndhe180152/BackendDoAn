using System;

namespace Backend.Application.DTOs.PaddyLots;

public class UpdatePaddyLotDto
{
    public int Id { get; set; }
    public int? OrganizationId { get; set; }
    public string LotType { get; set; } = null!;
    public int ProductVariantId { get; set; }
    public int? RiceVarietyId { get; set; }
    public int StatusId { get; set; }
    public int? SourceReceiptId { get; set; }
    public int? SourceMillingOrderId { get; set; }
    public int WarehouseId { get; set; }
    public int? LocationId { get; set; }
    public DateTime InboundDate { get; set; }
    public decimal InitialWeightKg { get; set; }
    public decimal RemainingWeightKg { get; set; }
    public decimal CostPricePerKg { get; set; }
    public string? QualityStatus { get; set; }
    public int? UpdatedBy { get; set; }
}
