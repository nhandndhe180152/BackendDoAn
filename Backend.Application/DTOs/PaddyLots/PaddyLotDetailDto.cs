using System;
using System.Collections.Generic;

namespace Backend.Application.DTOs.PaddyLots;

public class PaddyLotDetailDto
{
    public int Id { get; set; }
    public int? OrganizationId { get; set; }
    public string LotCode { get; set; } = null!;
    public string LotType { get; set; } = null!;
    public int ProductVariantId { get; set; }
    public string? SKU { get; set; }
    public string? ProductVariantName { get; set; }
    public int? RiceVarietyId { get; set; }
    public string? RiceVarietyName { get; set; }
    public int StatusId { get; set; }
    public string? StatusName { get; set; }
    public int? SourceReceiptId { get; set; }
    public int? SourceMillingOrderId { get; set; }
    public int WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public int? LocationId { get; set; }
    public DateTime InboundDate { get; set; }
    public decimal InitialWeightKg { get; set; }
    public decimal RemainingWeightKg { get; set; }
    public decimal CostPricePerKg { get; set; }
    public string? QualityStatus { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }
}
