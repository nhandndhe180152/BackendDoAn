using System.Threading;
using System.Threading.Tasks;
using Backend.Application.DTOs.ReturnToSuppliers;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

/// <summary>
/// Nghiệp vụ đơn trả hàng về nhà cung cấp (FE-16).
/// Trả hàng lỗi/hỏng (đang cách ly) về NCC: xuất tồn khỏi vị trí cách ly, giảm tồn lô,
/// và đảo ngược công nợ phải trả cho NCC tương ứng giá trị hàng trả.
/// </summary>
public interface IReturnToSupplierOrderService
{
    Task<ApiResponse> CreateAsync(CreateReturnToSupplierOrderDto dto, CancellationToken cancellationToken = default);
    Task<ApiResponse> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ApiResponse> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse> ApproveAsync(int id, string? note, CancellationToken cancellationToken = default);
    Task<ApiResponse> ConfirmAsync(int id, CancellationToken cancellationToken = default);
    Task<ApiResponse> CancelAsync(int id, string reason, CancellationToken cancellationToken = default);
}
