namespace Backend.Application.DTOs.Alerts;

/// <summary>
/// Body cho PUT /alerts/rules/{code}: bật/tắt một quy tắc cảnh báo.
/// </summary>
public class ToggleAlertRuleDto
{
    public bool Enabled { get; set; }
}
