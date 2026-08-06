using System;

namespace Backend.Application.DTOs.PaddyPurchaseReceipts;

/// <summary>
/// Dữ liệu bắt buộc khi chốt phiếu mua lúa và phát sinh công nợ phải trả.
/// </summary>
public class ConfirmPaddyPurchaseReceiptDto
{
    public DateTime? DueDate { get; set; }
}
