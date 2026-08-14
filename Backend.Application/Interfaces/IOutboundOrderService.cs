using System.Threading.Tasks;
using Backend.Application.DTOs.OutboundOrders;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IOutboundOrderService
{
    Task<ApiResponse> GetPagedAsync(OutboundOrderPagedQuery query);
    Task<ApiResponse> GetByIdAsync(int id);
    Task<ApiResponse> GetAllocationCandidatesAsync(int id);

    /// <summary>Gắn lot/vị trí cho từng item → PICKING</summary>
    Task<ApiResponse> AllocateAsync(int id, AllocateOutboundDto dto);

    /// <summary>Cập nhật số lượng thực lấy</summary>
    Task<ApiResponse> PickAsync(int id, PickOutboundDto dto);

    /// <summary>Đóng gói xong → PACKED (validate tất cả items đã pick đủ)</summary>
    Task<ApiResponse> ConfirmPackingAsync(int id, Backend.Application.DTOs.OutboundOrders.ConfirmPackingDto dto);

    /// <summary>
    /// Xác nhận xuất kho (DISPATCHED):
    /// - Giảm QuantityOnHand + QuantityReserved
    /// - Tạo InventoryTransaction per lot
    /// - Cập nhật SalesOrder → DELIVERING
    /// - Tạo PartyDebt/DebtTransaction nếu chưa trả đủ
    /// </summary>
    Task<ApiResponse> ConfirmDispatchAsync(int id, ConfirmDispatchDto dto);

    /// <summary>Xác nhận giao hàng thành công (DISPATCHED → COMPLETED)</summary>
    Task<ApiResponse> CompleteDeliveryAsync(int id, CompleteDeliveryDto dto);

    /// <summary>Giao hàng thất bại (DISPATCHED → DELIVERY_FAILED)</summary>
    Task<ApiResponse> FailDeliveryAsync(int id, FailDeliveryDto dto);

    /// <summary>Hủy OutboundOrder — giải phóng tồn nếu đang PICKING/PACKED</summary>
    Task<ApiResponse> CancelAsync(int id, string? reason = null);
    Task<ApiResponse> ForceUnlockAsync(int id, string reason);
}
