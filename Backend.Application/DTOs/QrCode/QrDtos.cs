using System;
using System.Collections.Generic;

namespace Backend.Application.DTOs.QrCode;

public class EnsureQrResponseDto
{
    public string EntityType { get; set; } = null!;
    public int EntityId { get; set; }
    public string DisplayCode { get; set; } = null!;
    public string QrCode { get; set; } = null!;
    public string QrPayload { get; set; } = null!;
    public bool Created { get; set; }
    public string QrImageUrl { get; set; } = null!;
    public string LabelUrl { get; set; } = null!;
}

public class RegenerateQrRequestDto
{
    public string Reason { get; set; } = null!;
}

public class QrResolveRequestDto
{
    public string Payload { get; set; } = null!;
    public QrContextDto? Context { get; set; }
}

public class QrContextDto
{
    public string Operation { get; set; } = null!; // LOOKUP, STORE_IN, MILLING_INPUT, OUTBOUND_PICKING, STOCKTAKE, STOCK_TRANSFER, QUALITY_INSPECTION, CUSTOMER_RETURN
    public int? ReferenceId { get; set; }
    public int? WarehouseId { get; set; }
}

public class QrResolveResponseDto
{
    public string EntityType { get; set; } = null!;
    public int EntityId { get; set; }
    public string QrCode { get; set; } = null!;
    public string DisplayCode { get; set; } = null!;
    
    // PaddyLot properties (null for Location)
    public string? LotType { get; set; }
    public QrProductVariantDto? ProductVariant { get; set; }
    public QrRiceVarietyDto? RiceVariety { get; set; }
    public QrLotStatusDto? Status { get; set; }
    public decimal? RemainingWeightKg { get; set; }
    public bool? IsQuarantined { get; set; }

    // Location properties (null for PaddyLot)
    public string? ZoneName { get; set; }
    public string? ShelfRow { get; set; }
    public string? ShelfLevel { get; set; }
    public string? SlotCode { get; set; }
    public decimal? MaxCapacityKg { get; set; }
    public decimal? CurrentOccupancyKg { get; set; }
    public decimal? FreeCapacityKg { get; set; }
    public bool? IsQuarantine { get; set; }
    public bool? IsActive { get; set; }

    // Shared Warehouse reference
    public QrWarehouseDto? Warehouse { get; set; }

    // Client routing helper
    public QrNavigationTargetDto NavigationTarget { get; set; } = null!;

    // Context validation results
    public QrContextValidationResultDto? ValidationResult { get; set; }
}

public class QrProductVariantDto
{
    public int Id { get; set; }
    public string Sku { get; set; } = null!;
    public string Name { get; set; } = null!;
}

public class QrRiceVarietyDto
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
}

public class QrLotStatusDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public bool IsSellable { get; set; }
}

public class QrWarehouseDto
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
}

public class QrNavigationTargetDto
{
    public string Type { get; set; } = null!; // PADDY_LOT_DETAIL, LOCATION_DETAIL
    public int Id { get; set; }
}

public class QrContextValidationResultDto
{
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}

public class BatchQrLabelPrintDto
{
    public List<int> Ids { get; set; } = new();
    public string Format { get; set; } = "PDF"; // PDF or PNG
    public string Template { get; set; } = "MEDIUM"; // SMALL, MEDIUM, LARGE
    public int CopiesPerLabel { get; set; } = 1;
}

public class QrLabelPreviewDto
{
    public List<QrLabelTemplateInfoDto> Templates { get; set; } = new();
    public List<string> Formats { get; set; } = new();
    public List<string> LabelTypes { get; set; } = new();
    public LabelPreviewDataDto? Label { get; set; }
}

public class LabelPreviewDataDto
{
    public string LabelType { get; set; } = null!;
    public int SubjectId { get; set; }
    public string Template { get; set; } = null!;
    public string QrPayload { get; set; } = null!;
    public string DisplayCode { get; set; } = null!;
    public string? ProductName { get; set; }
    public string? Sku { get; set; }
    public string? RiceVarietyName { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? PackageWeightKg { get; set; }
    public DateTime? InboundDate { get; set; }
    public string? WarehouseName { get; set; }
    public string? LocationName { get; set; }
    public bool IsQuarantined { get; set; }
}

public class QrLabelTemplateInfoDto
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public float WidthMm { get; set; }
    public float HeightMm { get; set; }
}

