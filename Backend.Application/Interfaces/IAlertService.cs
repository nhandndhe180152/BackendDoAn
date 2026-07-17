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
}
