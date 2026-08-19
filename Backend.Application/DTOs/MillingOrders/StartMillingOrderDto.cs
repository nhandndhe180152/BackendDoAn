using System.ComponentModel.DataAnnotations;

namespace Backend.Application.DTOs.MillingOrders;

/// <summary>
/// W14-J: DTO cho bước Bắt đầu lệnh xay — yêu cầu MachineRef để truy vết thiết bị.
/// OperatorId có thể được chốt ngay lúc Start hoặc muộn hơn ở bước Complete.
/// </summary>
public class StartMillingOrderDto
{
    /// <summary>
    /// Mã/tên máy xay sử dụng. Bắt buộc khi bắt đầu lệnh xay để ghi nhận truy vết thiết bị.
    /// Ví dụ: "MILL-01", "SX-A3", v.v.
    /// </summary>
    [Required(ErrorMessage = "Mã máy xay (MachineRef) là bắt buộc khi bắt đầu lệnh xay.")]
    [MaxLength(255, ErrorMessage = "Mã máy xay không được vượt quá 255 ký tự.")]
    public string MachineRef { get; set; } = null!;

    /// <summary>
    /// ID người vận hành máy xay. Có thể cung cấp tại Start hoặc Complete.
    /// Nếu không truyền ở Start, phải truyền ở Complete để hoàn tất truy vết.
    /// </summary>
    public int? OperatorId { get; set; }
}
