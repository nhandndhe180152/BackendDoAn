using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Backend.Application.DTOs.OutboundOrders;

public class ConfirmPackingDto
{
    [Required]
    public string QrCode { get; set; } = null!;

    /// <summary>
    /// Tổng khối lượng thực tế. Giữ lại cho client cũ; khi <see cref="Items"/>
    /// có dữ liệu thì tổng được tính lại từ các dòng.
    /// </summary>
    [Range(0, double.MaxValue, ErrorMessage = "Khối lượng thực tế không hợp lệ")]
    public decimal? ActualWeightKg { get; set; }

    /// <summary>
    /// Tên cân điện tử đã dùng. Chỉ gửi khi thực sự có số đến từ cân — nhập tay
    /// thì để null, tránh gắn nhãn sai nguồn số liệu.
    /// </summary>
    [MaxLength(255)]
    public string? ScaleDevice { get; set; }

    /// <summary>Khối lượng đóng gói theo từng dòng phiếu xuất.</summary>
    public List<ConfirmPackingItemDto>? Items { get; set; }
}

public class ConfirmPackingItemDto
{
    [Required]
    public int OutboundOrderItemId { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Khối lượng thực tế không hợp lệ")]
    public decimal? ActualWeightKg { get; set; }

    /// <summary>"SCALE" hoặc "MANUAL". Giá trị khác sẽ bị coi là MANUAL.</summary>
    [MaxLength(20)]
    public string? Source { get; set; }
}
