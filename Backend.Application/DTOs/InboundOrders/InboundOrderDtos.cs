using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Backend.Share.Entities;

namespace Backend.Application.DTOs.InboundOrders;

public class CreateInboundOrderDto
{
    [Required]
    public int WarehouseId { get; set; }

    public int? SupplierId { get; set; }

    public DateTime? ExpectedDate { get; set; }

    public string? Note { get; set; }

    [Required]
    [MinLength(1)]
    public List<CreateInboundOrderItemDto> Items { get; set; } = new();

    public int? CreatedBy { get; set; }
}

public class CreateInboundOrderItemDto
{
    [Required]
    public int ProductVariantId { get; set; }

    [Range(1, int.MaxValue)]
    public int QuantityOrdered { get; set; }

    [Range(0, double.MaxValue)]
    public decimal UnitCostPrice { get; set; }

    public string? Note { get; set; }
}

public class UpdateInboundOrderDto
{
    public int Id { get; set; }

    public int? SupplierId { get; set; }

    public DateTime? ExpectedDate { get; set; }

    public string? Note { get; set; }

    public List<UpdateInboundOrderItemDto> Items { get; set; } = new();

    public int? UpdatedBy { get; set; }
}

public class UpdateInboundOrderItemDto
{
    public int? Id { get; set; } // Null if adding new line

    [Required]
    public int ProductVariantId { get; set; }

    [Range(1, int.MaxValue)]
    public int QuantityOrdered { get; set; }

    [Range(0, double.MaxValue)]
    public decimal UnitCostPrice { get; set; }

    public string? Note { get; set; }
}

public class InboundOrderListDto
{
    public int Id { get; set; }
    public string POCode { get; set; } = null!;
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = null!;
    public int? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public int InboundOrderStatusId { get; set; }
    public string InboundOrderStatusName { get; set; } = null!;
    public decimal TotalAssetValue { get; set; }
    public DateTime? ExpectedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class InboundOrderDetailDto
{
    public int Id { get; set; }
    public string POCode { get; set; } = null!;
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = null!;
    public int? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public int InboundOrderStatusId { get; set; }
    public string InboundOrderStatusName { get; set; } = null!;
    public decimal TotalAssetValue { get; set; }
    public DateTime? ExpectedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedDate { get; set; }
    public List<InboundOrderItemDto> Items { get; set; } = new();
}

public class InboundOrderItemDto
{
    public int Id { get; set; }
    public int InboundOrderId { get; set; }
    public int? ProductVariantId { get; set; }
    public string? ProductVariantName { get; set; }
    public string? SKU { get; set; }
    public int QuantityOrdered { get; set; }
    public int QuantityReceived { get; set; }
    public decimal UnitCostPrice { get; set; }
    public decimal? ExpectedWeightKg { get; set; }
    public decimal? ActualWeightKg { get; set; }
    public bool QRScanned { get; set; }
    public string? Note { get; set; }

    // Logical state fields extracted from Note JSON
    public string ReceiptStatus { get; set; } = "Draft";
    public string? OverReceiveReason { get; set; }
    public string? WeightDiscrepancyReason { get; set; }
    public string? ExceptionDecision { get; set; } // Approved | Rejected
    public string? ExceptionReason { get; set; }
    public int? ConfirmedLocationId { get; set; }
    public string? ConfirmedLocationCode { get; set; }
    public string? PutawayOverrideReason { get; set; }
    public int? IotWeightLogId { get; set; }
    public int? QuantityEntered { get; set; }
}

public class StartReceiptDto
{
    [Required]
    public int InboundOrderItemId { get; set; }
}

public class ScanQrDto
{
    [Required]
    public string QrCode { get; set; } = null!;
}

public class RecordQuantityDto
{
    [Range(1, int.MaxValue)]
    public int QuantityReceived { get; set; }
    public string? Note { get; set; }
}

public class AttachWeightDto
{
    [Required]
    public int IotWeightLogId { get; set; }
}

public class ReviewExceptionDto
{
    [Required]
    [RegularExpression("^(Approve|Reject)$", ErrorMessage = "Decision must be Approve or Reject")]
    public string Decision { get; set; } = null!;

    [Required]
    public string Reason { get; set; } = null!;
}

public class SelectPutawayDto
{
    [Required]
    public int LocationId { get; set; }
    public bool IsOverride { get; set; }
    public string? OverrideReason { get; set; }
}

public class ConfirmReceiptDto
{
    [Required]
    public string OperationKey { get; set; } = null!;
}

public class PutawaySuggestionDto
{
    public int LocationId { get; set; }
    public string ZoneName { get; set; } = null!;
    public string? ShelfRow { get; set; }
    public string? ShelfLevel { get; set; }
    public string? SlotCode { get; set; }
    public double Score { get; set; }
    public int AvailableCapacity { get; set; }
    public int CurrentOccupancy { get; set; }
    public int Priority { get; set; }
    public bool CategoryMatch { get; set; }
    public Dictionary<string, double> ScoreBreakdown { get; set; } = new();
}
