using System.Threading.Tasks;
using Backend.Application.DTOs.InboundOrders;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IInboundOrderService
{
    Task<ApiResponse> GetPagedAsync(SearchQuery query);
    Task<ApiResponse> GetPagedAdvancedAsync(Backend.Domain.DTParameters.InboundOrderDTParameters parameters);
    Task<ApiResponse> GetByIdAsync(int id);
    Task<ApiResponse> CreateAsync(CreateInboundOrderDto dto);
    Task<ApiResponse> UpdateAsync(UpdateInboundOrderDto dto);
    Task<ApiResponse> SubmitAsync(int id);
    Task<ApiResponse> ApproveAsync(int id);
    Task<ApiResponse> RejectAsync(int id, string reason);
    Task<ApiResponse> CancelAsync(int id);

    // Receiving flow (Paddy / legacy)
    Task<ApiResponse> StartReceiptAsync(int orderId, StartReceiptDto dto);
    Task<ApiResponse> ScanQrAsync(int orderId, int receiptId, ScanQrDto dto);
    Task<ApiResponse> RecordQuantityAsync(int orderId, int receiptId, RecordQuantityDto dto);
    Task<ApiResponse> AttachWeightAsync(int orderId, int receiptId, AttachWeightDto dto);
    Task<ApiResponse> ReviewExceptionAsync(int orderId, int receiptId, ReviewExceptionDto dto);
    Task<ApiResponse> GetPutawaySuggestionsAsync(int orderId, int receiptId);
    Task<ApiResponse> SelectPutawayAsync(int orderId, int receiptId, SelectPutawayDto dto);
    Task<ApiResponse> ConfirmReceiptAsync(int orderId, int receiptId, ConfirmReceiptDto dto);
    Task<ApiResponse> GetReceiptsAsync(int orderId);

    // Chứng từ giao hàng (Delivery Note: ảnh + OCR)
    Task<ApiResponse> SaveDeliveryNoteAsync(int orderId, SaveDeliveryNoteDto dto);
    Task<ApiResponse> GetDeliveryNoteAsync(int orderId);

    // ── Non-paddy (PO → InboundOrder) flow ──────────────────────────────────
    /// <summary>
    /// Ghi số lượng thực nhận cho từng dòng InboundOrderItem (non-paddy).
    /// Có thể gọi nhiều lần (nhận từng đợt).
    /// </summary>
    Task<ApiResponse> ReceiveNonPaddyAsync(int id, ReceiveNonPaddyDto dto);

    /// <summary>
    /// Xác nhận hoàn tất nhập kho non-paddy trong 1 DB transaction:
    /// tăng QuantityOnHand, tạo InventoryTransaction, cập nhật PO status.
    /// </summary>
    Task<ApiResponse> ConfirmNonPaddyReceiveAsync(int id, ConfirmNonPaddyReceiveDto dto);
}
