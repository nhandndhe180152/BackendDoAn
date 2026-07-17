using System.Threading.Tasks;
using Backend.Application.DTOs.SalesOrders;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface ISalesOrderService
{
    Task<ApiResponse> GetPagedAsync(SalesOrderPagedQuery query);
    Task<ApiResponse> GetByIdAsync(int id);
    Task<ApiResponse> CreateAsync(CreateSalesOrderDto dto);
    Task<ApiResponse> UpdateAsync(UpdateSalesOrderDto dto);

    /// <summary>NEW → PENDING_CONFIRM</summary>
    Task<ApiResponse> ConfirmAsync(int id);

    /// <summary>PENDING_CONFIRM → RESERVED (giữ tồn kho)</summary>
    Task<ApiResponse> ReserveAsync(int id);

    /// <summary>Hủy đơn — giải phóng tồn đã giữ nếu trạng thái RESERVED</summary>
    Task<ApiResponse> CancelAsync(int id);

    /// <summary>Tạo OutboundOrder từ SalesOrder (RESERVED/PREPARING → PREPARING + OutboundOrder DRAFT)</summary>
    Task<ApiResponse> CreateOutboundAsync(int id);
}
