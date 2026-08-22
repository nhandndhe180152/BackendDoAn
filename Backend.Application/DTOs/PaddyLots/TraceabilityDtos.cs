using System;
using System.Collections.Generic;

namespace Backend.Application.DTOs.PaddyLots;

public class PaddyLotTraceabilityDto
{
    public int RequestedLotId { get; set; }
    public string RequestedLotCode { get; set; } = string.Empty;
    public bool IsTruncated { get; set; }
    public List<TraceabilityLotDto> RelatedLots { get; set; } = new();
    public List<TraceabilityPurchaseDto> Purchases { get; set; } = new();
    public List<TraceabilityInspectionDto> QualityInspections { get; set; } = new();
    public List<TraceabilityMillingDto> MillingOrders { get; set; } = new();
    public List<TraceabilityOutboundDto> OutboundSales { get; set; } = new();
    public List<TraceabilityCustomerFeedbackDto> CustomerFeedbacks { get; set; } = new();
    public List<TraceabilityCustomerReturnDto> CustomerReturns { get; set; } = new();
    public List<TraceabilityEventDto> Timeline { get; set; } = new();
    public TraceabilitySummaryDto Summary { get; set; } = new();
}

public class TraceabilityLotDto
{
    public int Id { get; set; }
    public string LotCode { get; set; } = string.Empty;
    public string LotType { get; set; } = string.Empty;
    public string RelationRole { get; set; } = string.Empty;
    public int ProductVariantId { get; set; }
    public string? Sku { get; set; }
    public string? ProductVariantName { get; set; }
    public int? RiceVarietyId { get; set; }
    public string? RiceVarietyName { get; set; }
    public int StatusId { get; set; }
    public string? StatusName { get; set; }
    public string? StatusCode { get; set; }
    public bool IsSellable { get; set; }
    public bool IsQuarantined { get; set; }
    public int WarehouseId { get; set; }
    public string? WarehouseCode { get; set; }
    public string? WarehouseName { get; set; }
    public int? LocationId { get; set; }
    public string? LocationCode { get; set; }
    public DateTime InboundDate { get; set; }
    public decimal InitialWeightKg { get; set; }
    public decimal RemainingWeightKg { get; set; }
    public string? QualityStatus { get; set; }
    public int? SourceReceiptId { get; set; }
    public int? SourceMillingOrderId { get; set; }
}

public class TraceabilityPurchaseDto
{
    public int ReceiptId { get; set; }
    public string ReceiptCode { get; set; } = string.Empty;
    public int? PaddyLotId { get; set; }
    public string? PaddyLotCode { get; set; }
    public int? ScheduleId { get; set; }
    public int FarmerId { get; set; }
    public string? FarmerCode { get; set; }
    public string? FarmerName { get; set; }
    public int? RiceVarietyId { get; set; }
    public string? RiceVarietyName { get; set; }
    public int WarehouseId { get; set; }
    public string? WarehouseCode { get; set; }
    public string? WarehouseName { get; set; }
    public DateTime ReceiptDate { get; set; }
    public decimal ActualWeightKg { get; set; }
    public int? BagCount { get; set; }
    public string? QualityJson { get; set; }
    public object? InitialQuality { get; set; }
    /// <summary>W14-J: Danh sách bão theo từng bao, kèm thông tin cân.</summary>
    public List<TraceabilityPurchaseBagDto> Bags { get; set; } = new();
}

/// <summary>W14-J: Thông tin truy vết cân từng bao lúa thu mua.</summary>
public class TraceabilityPurchaseBagDto
{
    public int BagId { get; set; }
    public int BagNo { get; set; }
    public decimal WeightKg { get; set; }
    /// <summary>Mã thiết bị cân (ví dụ: "SCALE-01"). Null nếu không có.</summary>
    public string? ScaleDeviceRef { get; set; }
    /// <summary>Phương thức cân: "SCALE" hoặc "MANUAL".</summary>
    public string? WeightCaptureMethod { get; set; }
    public DateTime? WeighedAt { get; set; }
    public int? WeighedBy { get; set; }
    public string? WeighedByName { get; set; }
}

public class TraceabilityInspectionDto
{
    public int InspectionId { get; set; }
    public int PaddyLotId { get; set; }
    public string? PaddyLotCode { get; set; }
    /// <summary>W14-J: Loại kiểm định (ví dụ: Receiving, Storage, OutboundException).</summary>
    public string? InspectionType { get; set; }
    public DateTime InspectedAt { get; set; }
    /// <summary>W14-J: Thời điểm hoàn thành kiểm định.</summary>
    public DateTime? CompletedAt { get; set; }
    public decimal? MoisturePercent { get; set; }
    public decimal? ImpurityPercent { get; set; }
    public string? MoldLevel { get; set; }
    public string? PestLevel { get; set; }
    public string? PackagingStatus { get; set; }
    public string? Handling { get; set; }
    public bool PassedInspection { get; set; }
    public string? ResultName { get; set; }
    public int? InspectorId { get; set; }
    public string? InspectorName { get; set; }
    public string? Note { get; set; }
    /// <summary>W14-J: Kết quả kiểm định từng bao.</summary>
    public List<TraceabilityInspectionBagResultDto> BagResults { get; set; } = new();
}

/// <summary>W14-J: Kết quả kiểm định cấp bao trong phiếu kiểm tra chất lượng.</summary>
public class TraceabilityInspectionBagResultDto
{
    public int BagResultId { get; set; }
    public int BagId { get; set; }
    public int BagNo { get; set; }
    public decimal WeightKg { get; set; }
    public decimal? MoisturePercent { get; set; }
    public string? QualityResult { get; set; }
    public string? Disposition { get; set; }
    public int? InspectorId { get; set; }
    public string? InspectorName { get; set; }
    public DateTime? InspectedAt { get; set; }
    public string? Handling { get; set; }
    public string? Note { get; set; }
}

public class TraceabilityMillingDto
{
    public int MillingOrderId { get; set; }
    public string MillingCode { get; set; } = string.Empty;
    public int StatusId { get; set; }
    public string? StatusName { get; set; }
    public string? StatusCode { get; set; }
    public int WarehouseId { get; set; }
    public string? WarehouseCode { get; set; }
    public string? WarehouseName { get; set; }
    public int? SalesOrderId { get; set; }
    public decimal YieldRateUsed { get; set; }
    public decimal ComputedPaddyKg { get; set; }
    public decimal TotalRiceOutputKg { get; set; }
    public decimal? ByproductKg { get; set; }
    public decimal? LossKg { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    /// <summary>W14-J: Mã máy xay đã sử dụng.</summary>
    public string? MachineRef { get; set; }
    /// <summary>W14-J: ID người vận hành máy xay.</summary>
    public int? OperatorId { get; set; }
    /// <summary>W14-J: Tên người vận hành máy xay.</summary>
    public string? OperatorName { get; set; }
    /// <summary>W14-J: Khối lượng lúa đầu vào thực tế (kg).</summary>
    public decimal? ActualPaddyInputKg { get; set; }
    /// <summary>W14-J: Tỷ lệ xuất gạo thực tế (actual rice / actual paddy input).</summary>
    public decimal? ActualYieldRate { get; set; }
    public List<TraceabilityMillingInputDto> Inputs { get; set; } = new();
    public List<TraceabilityMillingOutputDto> Outputs { get; set; } = new();
}

public class TraceabilityMillingInputDto
{
    public int MillingOrderInputId { get; set; }
    public int PaddyLotId { get; set; }
    public string? PaddyLotCode { get; set; }
    public string? LotType { get; set; }
    public int ProductVariantId { get; set; }
    public string? Sku { get; set; }
    public int? LocationId { get; set; }
    public string? LocationCode { get; set; }
    public decimal? ReservedWeightKg { get; set; }
    public decimal ConsumedWeightKg { get; set; }
    public string? Note { get; set; }
}

public class TraceabilityMillingOutputDto
{
    public int MillingOrderOutputId { get; set; }
    public int? OutputLotId { get; set; }
    public string? OutputLotCode { get; set; }
    public int ProductVariantId { get; set; }
    public string? Sku { get; set; }
    public string? ProductVariantName { get; set; }
    public string OutputType { get; set; } = string.Empty;
    public decimal OutputWeightKg { get; set; }
    public int? BagCount { get; set; }
    public bool IsByproduct { get; set; }
    public int? LocationId { get; set; }
    public string? LocationCode { get; set; }
}

public class TraceabilityOutboundDto
{
    public int OutboundOrderId { get; set; }
    public int OutboundStatusId { get; set; }
    public string? OutboundStatusName { get; set; }
    public string? OutboundStatusCode { get; set; }
    public DateTime? CompletedDate { get; set; }
    public int WarehouseId { get; set; }
    public string? WarehouseCode { get; set; }
    public string? WarehouseName { get; set; }
    public int SalesOrderId { get; set; }
    public string? SalesOrderCode { get; set; }
    public int? SalesOrderStatusId { get; set; }
    public string? SalesOrderStatusName { get; set; }
    public string? SalesOrderStatusCode { get; set; }
    public string? Channel { get; set; }
    public DateTime? SalesOrderDate { get; set; }
    public int? CustomerId { get; set; }
    public string? CustomerCode { get; set; }
    public string? CustomerName { get; set; }
    public List<TraceabilityOutboundAllocationDto> Allocations { get; set; } = new();
}

public class TraceabilityOutboundAllocationDto
{
    public int AllocationId { get; set; }
    public int OutboundOrderItemId { get; set; }
    public int? PaddyLotId { get; set; }
    public string? PaddyLotCode { get; set; }
    public int ProductVariantId { get; set; }
    public string? Sku { get; set; }
    public string? ProductVariantName { get; set; }
    public int LocationId { get; set; }
    public string? LocationCode { get; set; }
    public decimal QuantityAllocatedKg { get; set; }
    public decimal QuantityPickedKg { get; set; }
}

public class TraceabilityCustomerFeedbackDto
{
    public int FeedbackId { get; set; }
    public int SalesOrderId { get; set; }
    public int OutboundOrderId { get; set; }
    public int? OutboundOrderItemId { get; set; }
    public int? PaddyLotBagAllocationId { get; set; }
    public int? BagId { get; set; }
    public int? BagNo { get; set; }
    public int? PaddyLotId { get; set; }
    public string? PaddyLotCode { get; set; }
    public string FeedbackType { get; set; } = string.Empty;
    public string? Severity { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ResolutionStatus { get; set; } = string.Empty;
    public string? ResolutionNote { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public int? CustomerReturnOrderId { get; set; }
    public string? CustomerReturnOrderCode { get; set; }
}

public class TraceabilityCustomerReturnDto
{
    public int CustomerReturnOrderId { get; set; }
    public string ReturnCode { get; set; } = string.Empty;
    public int OutboundOrderId { get; set; }
    public int SalesOrderId { get; set; }
    public int? CustomerFeedbackId { get; set; }
    public int? CustomerId { get; set; }
    public int WarehouseId { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string? ReturnReason { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? InspectedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public decimal ApprovedCreditAmount { get; set; }
    public decimal DebtReductionAmount { get; set; }
    public decimal RefundPendingAmount { get; set; }
    public decimal RefundedAmount { get; set; }
    public string RefundStatus { get; set; } = string.Empty;
    public DateTime? RefundedAt { get; set; }
    public List<TraceabilityCustomerReturnItemDto> Items { get; set; } = new();
}

public class TraceabilityCustomerReturnItemDto
{
    public int CustomerReturnOrderItemId { get; set; }
    public int? ProductVariantId { get; set; }
    public string? ProductVariantName { get; set; }
    public string? Sku { get; set; }
    public List<TraceabilityCustomerReturnAllocationDto> Allocations { get; set; } = new();
}

public class TraceabilityCustomerReturnAllocationDto
{
    public int ReturnAllocationId { get; set; }
    public int? OutboundOrderItemAllocationId { get; set; }
    public int PaddyLotId { get; set; }
    public string? PaddyLotCode { get; set; }
    public int ProductVariantId { get; set; }
    public decimal QuantityReturned { get; set; }
    public decimal QuantityReceived { get; set; }
    public decimal QuantityGood { get; set; }
    public decimal QuantityDamaged { get; set; }
    public decimal QuantityRejected { get; set; }
    public string Disposition { get; set; } = string.Empty;
    public int? RestockLocationId { get; set; }
    public int? QuarantineLocationId { get; set; }
    public int? RejectedLocationId { get; set; }
    public decimal CreditAmount { get; set; }
}

public class TraceabilityEventDto
{
    public DateTime EventAt { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string ReferenceType { get; set; } = string.Empty;
    public int ReferenceId { get; set; }
    public string? ReferenceCode { get; set; }
    public List<int> PaddyLotIds { get; set; } = new();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal? QuantityKg { get; set; }
    public string? Status { get; set; }
    public int Sequence { get; set; }
}

public class TraceabilitySummaryDto
{
    public int RelatedLotCount { get; set; }
    public int PurchaseReceiptCount { get; set; }
    public int InspectionCount { get; set; }
    public int MillingOrderCount { get; set; }
    public int OutboundOrderCount { get; set; }
    public int FeedbackCount { get; set; }
    public int CustomerReturnCount { get; set; }
    public decimal PurchasedWeightKg { get; set; }
    public decimal MillingInputWeightKg { get; set; }
    public decimal MillingRiceOutputWeightKg { get; set; }
    public decimal MillingByproductWeightKg { get; set; }
    public decimal MillingLossWeightKg { get; set; }
    public decimal AllocatedOutboundWeightKg { get; set; }
    public decimal DispatchedWeightKg { get; set; }
    public decimal ReturnedWeightKg { get; set; }
    public decimal RestockedReturnWeightKg { get; set; }
    public decimal QuarantinedReturnWeightKg { get; set; }
    public decimal RejectedReturnWeightKg { get; set; }
    public decimal RefundAmount { get; set; }
    public decimal CurrentRemainingWeightKg { get; set; }
}
