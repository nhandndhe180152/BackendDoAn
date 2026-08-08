using System;
using System.Collections.Generic;

namespace Backend.Application.DTOs.ReturnToSuppliers;

/// <summary>
/// DTO tạo đơn trả hàng về nhà cung cấp (FE-16).
/// Hàng trả đi lấy từ vị trí CÁCH LY (quarantine) — thường là hàng non-paddy lỗi/hỏng từ đơn mua (PO).
/// </summary>
public class CreateReturnToSupplierOrderDto
{
    public int WarehouseId { get; set; }
    public int SupplierId { get; set; }
    public int? InboundOrderId { get; set; }
    public string? Note { get; set; }
    public List<CreateReturnToSupplierOrderItemDto> Items { get; set; } = new();
}

public class CreateReturnToSupplierOrderItemDto
{
    public int ProductVariantId { get; set; }
    public int QuarantineLocationId { get; set; }
    public int QuantityToReturn { get; set; }
    public string? DamageReason { get; set; }
    public string? Note { get; set; }
}

public class ReturnToSupplierOrderItemDto
{
    public int Id { get; set; }
    public int? ProductVariantId { get; set; }
    public string? ProductVariantName { get; set; }
    public string? SKU { get; set; }
    public int? QuarantineLocationId { get; set; }
    public string? LocationCode { get; set; }
    public int QuantityToReturn { get; set; }
    public int QuantityActualReturned { get; set; }
    public string? DamageReason { get; set; }
    public string? Note { get; set; }
}

public class ReturnToSupplierOrderDetailDto
{
    public int Id { get; set; }
    public string ReturnCode { get; set; } = string.Empty;
    public int WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public int SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public int StatusId { get; set; }
    public string? StatusName { get; set; }
    public string? StatusCode { get; set; }
    public int? InboundOrderId { get; set; }
    public string? Note { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public DateTime? CreatedDate { get; set; }
    public List<ReturnToSupplierOrderItemDto> Items { get; set; } = new();
}
