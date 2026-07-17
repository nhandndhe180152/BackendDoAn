using System.Threading.Tasks;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

/// <summary>
/// Đọc và xử lý Cảnh báo (Alert) cho màn SCR-21. Alert được các background job sinh ra;
/// service này phục vụ liệt kê, xem chi tiết, ghi nhận (acknowledge), xử lý (resolve) và xoá mềm.
/// </summary>
public interface IAlertService
{
    Task<ApiResponse> GetPagedAsync(DTParameter parameters);
    Task<ApiResponse> GetByIdAsync(int id);
    Task<ApiResponse> GetSummaryAsync();
    Task<ApiResponse> AcknowledgeAsync(int id, int userId);
    Task<ApiResponse> ResolveAsync(int id, int userId);
    Task<ApiResponse> SoftDeleteAsync(int id);

    /// <summary>Đánh dấu tất cả cảnh báo đang mở (OPEN) là đã đọc/ghi nhận (ACKNOWLEDGED).</summary>
    Task<ApiResponse> MarkAllReadAsync(int userId);

    /// <summary>Danh sách quy tắc cảnh báo + trạng thái bật/tắt (khối "Quy tắc cảnh báo").</summary>
    Task<ApiResponse> GetRulesAsync();

    /// <summary>Bật/tắt một quy tắc cảnh báo theo mã.</summary>
    Task<ApiResponse> ToggleRuleAsync(string code, bool enabled, int userId);
}
