using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Backend.Application.DTOs.InboundOrders;

/// <summary>
/// Ghi số lượng thực nhận cho từng dòng InboundOrderItem (luồng non-paddy).
/// Có thể gọi nhiều lần (nhận từng đợt).
/// </summary>
public class ReceiveNonPaddyDto
{
    [Required]
    [MinLength(1, ErrorMessage = "Phải có ít nhất 1 dòng nhận hàng.")]
    public List<ReceiveNonPaddyItemDto> Items { get; set; } = new();

    public string? Note { get; set; }
}

public class ReceiveNonPaddyItemDto
{
    [Required]
    public int InboundOrderItemId { get; set; }

    [Required]
    public int LocationId { get; set; }

    [Range(0.001, double.MaxValue, ErrorMessage = "Số lượng nhận phải lớn hơn 0.")]
    public decimal QuantityReceived { get; set; }

    public decimal? ActualWeightKg { get; set; }

    public string? Note { get; set; }

    /// <summary>
    /// Nếu true → tạo PaddyLot(LotType=PURCHASED_GOOD) để truy xuất theo batch.
    /// Mặc định false (không tạo lot).
    /// </summary>
    public bool CreateBatchLot { get; set; } = false;
}

/// <summary>
/// Xác nhận hoàn tất nhập kho non-paddy — trigger tăng QuantityOnHand.
/// </summary>
public class ConfirmNonPaddyReceiveDto
{
    public string? Note { get; set; }
}
