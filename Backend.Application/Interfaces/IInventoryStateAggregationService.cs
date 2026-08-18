using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Backend.Application.Interfaces;

public class InventoryStateAggregateDto
{
    public int WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = null!;
    public string WarehouseName { get; set; } = null!;
    public int ProductVariantId { get; set; }
    public string SKU { get; set; } = null!;
    public string ProductName { get; set; } = null!;
    public string ProductVariantName { get; set; } = null!;
    public int? PaddyLotId { get; set; }
    public string? LotCode { get; set; }
    public string? LotType { get; set; }
    public string? LotStatusName { get; set; }
    public int? RiceVarietyId { get; set; }
    public bool IsVariantByproduct { get; set; }
    public int? LocationId { get; set; }
    public string? LocationCode { get; set; }
    public decimal TotalOnHandKg { get; set; }
    public decimal SellableOnHandKg { get; set; }
    public decimal ReservedKg { get; set; }
    public decimal ReservedSellableKg { get; set; }
    public decimal QuarantinedKg { get; set; }
    public decimal OtherBlockedKg { get; set; }
    public decimal AvailableKg { get; set; }
}

public interface IInventoryStateAggregationService
{
    Task<List<InventoryStateAggregateDto>> GetAggregatesAsync(int? warehouseId, int? productVariantId, CancellationToken cancellationToken);
}
