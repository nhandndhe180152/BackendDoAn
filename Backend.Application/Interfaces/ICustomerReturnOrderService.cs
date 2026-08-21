using System.Threading;
using System.Threading.Tasks;
using Backend.Application.DTOs.CustomerReturns;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface ICustomerReturnOrderService
{
    Task<ApiResponse> CreateAsync(CreateCustomerReturnOrderDto dto, CancellationToken cancellationToken = default);
    Task<ApiResponse> UpdateAsync(UpdateCustomerReturnOrderDto dto, CancellationToken cancellationToken = default);
    Task<ApiResponse> SubmitAsync(int id, CancellationToken cancellationToken = default);
    Task<ApiResponse> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ApiResponse> GetPagedAsync(CustomerReturnOrderPagedQuery query, CancellationToken cancellationToken = default);
    Task<ApiResponse> GetReturnSourcesAsync(CustomerReturnSourceQuery query, CancellationToken cancellationToken = default);
    Task<ApiResponse> GetReturnSourceByIdAsync(int outboundOrderId, CancellationToken cancellationToken = default);
    Task<ApiResponse> ApproveAsync(int id, string? note, CancellationToken cancellationToken = default);
    Task<ApiResponse> RejectAsync(int id, string reason, CancellationToken cancellationToken = default);
    Task<ApiResponse> ReceiveAsync(ReceiveCustomerReturnOrderDto dto, CancellationToken cancellationToken = default);
    Task<ApiResponse> InspectAsync(InspectCustomerReturnOrderDto dto, CancellationToken cancellationToken = default);
    Task<ApiResponse> GetImpactPreviewAsync(int id, CancellationToken cancellationToken = default);
    Task<ApiResponse> ConfirmAsync(int id, CancellationToken cancellationToken = default);
    Task<ApiResponse> RegisterRefundAsync(int id, RegisterCustomerReturnRefundDto dto, CancellationToken cancellationToken = default);
    Task<ApiResponse> CancelAsync(int id, string reason, CancellationToken cancellationToken = default);
}
