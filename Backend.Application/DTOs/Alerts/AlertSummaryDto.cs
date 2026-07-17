namespace Backend.Application.DTOs.Alerts;

/// <summary>
/// Tổng hợp số lượng cảnh báo theo trạng thái/mức độ — phục vụ KPI màn Cảnh báo & Dashboard.
/// </summary>
public class AlertSummaryDto
{
    public int TotalOpen { get; set; }
    public int TotalAcknowledged { get; set; }
    public int TotalResolved { get; set; }
    public int OpenCritical { get; set; }
    public int OpenWarning { get; set; }
    public int OpenInfo { get; set; }
}
