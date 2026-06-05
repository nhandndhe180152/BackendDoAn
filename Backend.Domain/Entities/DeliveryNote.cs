using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class DeliveryNote : EntityAuditBase<int>
{
    public int? InboundOrderId { get; set; }
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
    public bool IsConfirmed { get; set; }
    
    public virtual FileUpload? OriginalImageFile { get; set; }
    public virtual InboundOrder? InboundOrder { get; set; }
}
