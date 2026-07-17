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

    /// <summary>Hủy đơn — chỉ có thể hủy khi chưa có phiếu xuất nào được xác nhận. QuantityReserved do OutboundOrder quản lý.</summary>
    Task<ApiResponse> CancelAsync(int id);

    /// <summary>Tạo OutboundOrder từ SalesOrder (RESERVED/PREPARING → PREPARING + OutboundOrder DRAFT)</summary>
    Task<ApiResponse> CreateOutboundAsync(int id);

    /// <summary>H1: Xác nhận giao hàng hoàn tất — DELIVERING → Hoàn tất. Cho phép Dashboard dịch vụ ghi nhận doanh thu.</summary>
    Task<ApiResponse> CompleteDeliveryAsync(int id);
}
