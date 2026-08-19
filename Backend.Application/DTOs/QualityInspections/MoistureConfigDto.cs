namespace Backend.Application.DTOs.QualityInspections;

/// <summary>
/// W14-J: DTO cấu hình ngưỡng độ ẩm từ SystemConfig, phục vụ cho Frontend hiển thị cảnh báo thay vì hard-code.
/// </summary>
public class MoistureConfigDto
{
    public decimal? ReceivingMoistureMinPercent { get; set; }
    public decimal? ReceivingMoistureMaxPercent { get; set; }
    public decimal? StorageQcMoistureWarningPercent { get; set; }
    public string SourceNote { get; set; } = "Giá trị được đọc từ SystemConfig — không hard-code tại frontend.";
}
