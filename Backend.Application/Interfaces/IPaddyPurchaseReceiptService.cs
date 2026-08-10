using System.Threading.Tasks;
using Backend.Application.DTOs.PaddyPurchaseReceipts;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IPaddyPurchaseReceiptService : IServiceBase<int, CreatePaddyPurchaseReceiptDto, UpdatePaddyPurchaseReceiptDto, DTParameter>
{
    /// <summary>
    /// Chốt phiếu: sinh PaddyLot, tạo InboundOrder, cập nhật tồn, ghi công nợ nếu có.
    /// </summary>
    Task<ApiResponse> ConfirmReceiptAsync(int receiptId, ConfirmPaddyPurchaseReceiptDto dto, int confirmedById);

    /// <summary>
    /// Danh sách biến thể sản phẩm lúa (không phải phụ phẩm) để chọn trên phiếu mua — FE lọc theo giống lúa.
    /// </summary>
    Task<ApiResponse> GetProductVariantLookupAsync();

    /// <summary>
    /// Tính toán và tự động cập nhật trạng thái lịch hẹn dựa trên các phiếu mua thuộc lịch.
    /// </summary>
    Task UpdateScheduleStatusAsync(int scheduleId, int userId);
}
