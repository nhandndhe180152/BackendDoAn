namespace Backend.Application.DTOs.Alerts;

/// <summary>
/// Một quy tắc cảnh báo (bật/tắt) hiển thị ở khối "Quy tắc cảnh báo" trên màn SCR-21.
/// </summary>
public class AlertRuleDto
{
    public string Code { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public bool Enabled { get; set; }
}
