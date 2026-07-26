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

    [Range(0.001, double.MaxValue)]
    public decimal QuantityOrdered { get; set; }

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

    [Range(0.001, double.MaxValue)]
    public decimal QuantityOrdered { get; set; }

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
    public string? SourceType { get; set; }
    public int? PaddyPurchaseReceiptId { get; set; }
    public string? PaddyPurchaseReceiptCode { get; set; }
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
    public string? SourceType { get; set; }
    public int? PaddyPurchaseReceiptId { get; set; }
    public string? PaddyPurchaseReceiptCode { get; set; }
    public DateTime CreatedDate { get; set; }
    public List<InboundOrderItemDto> Items { get; set; } = new();

    /// <summary>Chứng từ giao hàng (ảnh + thông tin OCR) gắn với phiếu nhập, null nếu chưa có.</summary>
    public DeliveryNoteDto? DeliveryNote { get; set; }
}

public class InboundOrderItemDto
{
    public int Id { get; set; }
    public int InboundOrderId { get; set; }
    public int? ProductVariantId { get; set; }
    public int? PaddyLotId { get; set; }
    public string? PaddyLotCode { get; set; }
    public string? PaddyQualityStatus { get; set; }
    public string? ProductVariantName { get; set; }
    public string? SKU { get; set; }
    public decimal QuantityOrdered { get; set; }
    public decimal QuantityReceived { get; set; }
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
    public decimal? QuantityEntered { get; set; }
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
    [Range(0.001, double.MaxValue)]
    public decimal QuantityReceived { get; set; }
    public string? Note { get; set; }
}

public class AttachWeightDto
{
    /// <summary>
    /// Khối lượng cân thực tế (kg) do app đọc trực tiếp từ cân qua Bluetooth (BLE) và gửi lên.
    /// Backend không còn lưu bằng chứng cân từ thiết bị IoT — số cân được chốt thẳng vào phiếu.
    /// </summary>
    [Range(0.001, double.MaxValue, ErrorMessage = "Khối lượng cân phải lớn hơn 0.")]
    public decimal ActualWeightKg { get; set; }
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
    public decimal? WeightKg { get; set; }
}

public class ConfirmReceiptDto
{
    [Required]
    public string OperationKey { get; set; } = null!;
}

/// <summary>
/// Dữ liệu lưu/gắn chứng từ giao hàng (Delivery Note) vào phiếu nhập.
/// Ảnh đã được upload trước qua /file-manager/upload-by-category, ở đây chỉ truyền OriginalImageFileId.
/// </summary>
public class SaveDeliveryNoteDto
{
    /// <summary>Id của FileUpload (ảnh chứng từ đã upload lên Cloudinary).</summary>
    [Required]
    public int OriginalImageFileId { get; set; }

    public string? TrackingCode { get; set; }
    public string? CarrierName { get; set; }
    public string? SenderName { get; set; }
    public string? SenderPhone { get; set; }
    public string? SenderAddress { get; set; }
    public string? ReceiverName { get; set; }
    public string? ReceiverPhone { get; set; }
    public string? ReceiverAddress { get; set; }
    public decimal? DeclaredWeight { get; set; }
    public decimal? CODAmount { get; set; }

    /// <summary>Văn bản OCR trích xuất từ ảnh chứng từ (nếu có).</summary>
    public string? RawOcrText { get; set; }

    /// <summary>Đánh dấu đã xác nhận chứng từ.</summary>
    public bool IsConfirmed { get; set; }
}

/// <summary>Chứng từ giao hàng trả về cho client, kèm URL ảnh.</summary>
public class DeliveryNoteDto
{
    public int Id { get; set; }
    public int InboundOrderId { get; set; }
    public string? TrackingCode { get; set; }
    public string? CarrierName { get; set; }
    public string? SenderName { get; set; }
    public string? SenderPhone { get; set; }
    public string? SenderAddress { get; set; }
    public string? ReceiverName { get; set; }
    public string? ReceiverPhone { get; set; }
    public string? ReceiverAddress { get; set; }
    public decimal? DeclaredWeight { get; set; }
    public decimal? CODAmount { get; set; }
    public string? RawOcrText { get; set; }
    public int? OriginalImageFileId { get; set; }

    /// <summary>URL ảnh chứng từ để hiển thị/xem lại.</summary>
    public string? ImageUrl { get; set; }
    public bool IsConfirmed { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class PutawaySuggestionDto
{
    public int LocationId { get; set; }
    public string ZoneName { get; set; } = null!;
    public string? ShelfRow { get; set; }
    public string? ShelfLevel { get; set; }
    public string? SlotCode { get; set; }
    public double Score { get; set; }
    public decimal AvailableCapacity { get; set; }
    public decimal CurrentOccupancy { get; set; }
    public decimal RecommendedWeightKg { get; set; }
    public bool CanFitWhole { get; set; }
    public bool IsQuarantine { get; set; }
    public int Priority { get; set; }
    public bool CategoryMatch { get; set; }
    public Dictionary<string, double> ScoreBreakdown { get; set; } = new();
}
