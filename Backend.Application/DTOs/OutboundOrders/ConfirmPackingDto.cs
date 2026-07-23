using System.ComponentModel.DataAnnotations;

namespace Backend.Application.DTOs.OutboundOrders;

public class ConfirmPackingDto
{
    [Required]
    public string QrCode { get; set; } = null!;

    [Range(0, double.MaxValue, ErrorMessage = "Khối lượng thực tế không hợp lệ")]
    public decimal? ActualWeightKg { get; set; }

    public string? ScaleDevice { get; set; }
}
