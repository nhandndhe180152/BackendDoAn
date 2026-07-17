using System.Threading.Tasks;
using Backend.Application.DTOs.PurchaseOrders;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IPurchaseOrderService
{
    Task<ApiResponse> GetPagedAsync(PurchaseOrderPagedQuery query);
    Task<ApiResponse> GetByIdAsync(int id);
    Task<ApiResponse> CreateAsync(CreatePurchaseOrderDto dto);
    Task<ApiResponse> UpdateAsync(UpdatePurchaseOrderDto dto);

    /// <summary>DRAFT → CONFIRMED</summary>
    Task<ApiResponse> ConfirmAsync(int id);

    /// <summary>
    /// Tạo InboundOrder (SourceType=PO) từ PurchaseOrder.
    /// PO phải ở CONFIRMED hoặc PARTIALLY_RECEIVED.
    /// </summary>
    Task<ApiResponse> CreateInboundAsync(int id);

    /// <summary>Hủy PO — chỉ khi DRAFT hoặc CONFIRMED</summary>
    Task<ApiResponse> CancelAsync(int id);
}
