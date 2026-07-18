using System.Threading.Tasks;
using Backend.Application.DTOs.OutboundOrders;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IOutboundOrderService
{
    Task<ApiResponse> GetPagedAsync(OutboundOrderPagedQuery query);
    Task<ApiResponse> GetByIdAsync(int id);

    /// <summary>Gắn lot/vị trí cho từng item → PICKING</summary>
    Task<ApiResponse> AllocateAsync(int id, AllocateOutboundDto dto);

    /// <summary>Cập nhật số lượng thực lấy</summary>
    Task<ApiResponse> PickAsync(int id, PickOutboundDto dto);

    /// <summary>Đóng gói xong → PACKED (validate tất cả items đã pick đủ)</summary>
    Task<ApiResponse> ConfirmPackingAsync(int id);

    /// <summary>
    /// Xác nhận xuất kho (DISPATCHED):
    /// - Giảm QuantityOnHand + QuantityReserved
    /// - Tạo InventoryTransaction per lot
    /// - Cập nhật SalesOrder → DELIVERING
    /// - Tạo PartyDebt/DebtTransaction nếu chưa trả đủ
    /// </summary>
    Task<ApiResponse> ConfirmDispatchAsync(int id, ConfirmDispatchDto dto);

    /// <summary>Hủy OutboundOrder — giải phóng tồn nếu đang PICKING/PACKED</summary>
    Task<ApiResponse> CancelAsync(int id);
}
