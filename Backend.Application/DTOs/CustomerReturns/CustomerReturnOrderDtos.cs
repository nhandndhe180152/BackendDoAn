using System;
using System.Collections.Generic;

namespace Backend.Application.DTOs.CustomerReturns;

public class CreateCustomerReturnOrderDto
{
    public int WarehouseId { get; set; }
    public int CustomerId { get; set; }
    public int OutboundOrderId { get; set; }
    public string? ReturnReason { get; set; }
    public string? Note { get; set; }
    public List<CreateCustomerReturnOrderItemDto> Items { get; set; } = new();
}

public class CreateCustomerReturnOrderItemDto
{
    public int OutboundOrderItemId { get; set; }
    public int ProductVariantId { get; set; }
    public decimal QuantityReturned { get; set; }
    public List<CreateCustomerReturnOrderItemAllocationDto> Allocations { get; set; } = new();
}

public class CreateCustomerReturnOrderItemAllocationDto
{
    public int OutboundOrderItemAllocationId { get; set; }
    public decimal QuantityReturned { get; set; }
}

public class UpdateCustomerReturnOrderDto
{
    public int Id { get; set; }
    public string? ReturnReason { get; set; }
    public string? Note { get; set; }
    public List<CreateCustomerReturnOrderItemDto> Items { get; set; } = new();
}

public class InspectCustomerReturnOrderDto
{
    public int Id { get; set; }
    public List<InspectCustomerReturnOrderItemDto> Items { get; set; } = new();
}

public class InspectCustomerReturnOrderItemDto
{
    public int CustomerReturnOrderItemId { get; set; }
    public string QualityStatus { get; set; } = null!; // GOOD | DAMAGED | EXPIRED
    public string? DamageReason { get; set; }
    public List<InspectCustomerReturnOrderItemAllocationDto> Allocations { get; set; } = new();
}

public class InspectCustomerReturnOrderItemAllocationDto
{
    public int ReturnAllocationId { get; set; }
    public decimal QuantityGood { get; set; }
    public decimal QuantityDamaged { get; set; }
    public decimal QuantityRejected { get; set; }
    public decimal CreditQuantity { get; set; }
    public int? RestockLocationId { get; set; }
    public int? QuarantineLocationId { get; set; }
    public string? Note { get; set; }
}

public class CustomerReturnOrderListDto
{
    public int Id { get; set; }
    public string ReturnCode { get; set; } = null!;
    public string? ReturnReason { get; set; }
    public string? Note { get; set; }
    
    public int StatusId { get; set; }
    public string StatusCode { get; set; } = null!;
    public string StatusName { get; set; } = null!;
    
    public int? OutboundOrderId { get; set; }
    public string? OutboundOrderCode { get; set; }
    
    public int? CustomerId { get; set; }
    public string? CustomerCode { get; set; }
    public string? CustomerName { get; set; }
    
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = null!;
    
    public decimal ApprovedCreditAmount { get; set; }
    public decimal DebtReductionAmount { get; set; }
    public decimal RefundPendingAmount { get; set; }
    
    public DateTime? ApprovedDate { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public string? ConfirmedByName { get; set; }
    public DateTime? CompletedDate { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class CustomerReturnOrderDetailDto : CustomerReturnOrderListDto
{
    public List<CustomerReturnOrderItemDetailDto> Items { get; set; } = new();
}

public class CustomerReturnOrderItemDetailDto
{
    public int Id { get; set; }
    public int? ProductVariantId { get; set; }
    public string? ProductVariantName { get; set; }
    public string? SKU { get; set; }
    
    public decimal QuantityReturned { get; set; }
    public decimal QuantityGood { get; set; }
    public decimal QuantityDamaged { get; set; }
    public string QualityStatus { get; set; } = null!;
    public string? DamageReason { get; set; }
    public string? Note { get; set; }
    
    public List<CustomerReturnOrderItemAllocationDetailDto> Allocations { get; set; } = new();
}

public class CustomerReturnOrderItemAllocationDetailDto
{
    public int Id { get; set; }
    public int OutboundOrderItemAllocationId { get; set; }
    public int PaddyLotId { get; set; }
    public string PaddyLotCode { get; set; } = null!;
    public int ProductVariantId { get; set; }
    public string SKU { get; set; } = null!;
    
    public int? OriginalLocationId { get; set; }
    public string? OriginalLocationCode { get; set; }
    
    public decimal QuantityReturned { get; set; }
    public decimal QuantityGood { get; set; }
    public decimal QuantityDamaged { get; set; }
    public decimal QuantityRejected { get; set; }
    public decimal CreditQuantity { get; set; }
    
    public int? RestockLocationId { get; set; }
    public string? RestockLocationCode { get; set; }
    public int? QuarantineLocationId { get; set; }
    public string? QuarantineLocationCode { get; set; }
    
    public decimal UnitCreditPrice { get; set; }
    public decimal CreditAmount { get; set; }
    public string? Note { get; set; }
}

public class CustomerReturnImpactPreviewDto
{
    public int CustomerReturnOrderId { get; set; }
    public decimal ApprovedCreditAmount { get; set; }
    public decimal CurrentReceivableBalance { get; set; }
    public decimal DebtReductionAmount { get; set; }
    public decimal RefundPendingAmount { get; set; }
    public List<CustomerReturnInventoryImpactDto> InventoryImpact { get; set; } = new();
}

public class CustomerReturnInventoryImpactDto
{
    public int PaddyLotId { get; set; }
    public string PaddyLotCode { get; set; } = null!;
    public int ProductVariantId { get; set; }
    public string SKU { get; set; } = null!;
    public decimal GoodQuantityKg { get; set; }
    public decimal DamagedQuantityKg { get; set; }
    public decimal RejectedQuantityKg { get; set; }
    public int? RestockLocationId { get; set; }
    public int? QuarantineLocationId { get; set; }
}

public class CustomerReturnOrderPagedQuery
{
    public string? Keyword { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
