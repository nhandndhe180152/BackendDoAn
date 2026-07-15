using System.Threading.Tasks;
using Backend.Application.DTOs.PaddyPurchaseReceipts;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IPaddyPurchaseReceiptService : IServiceBase<int, CreatePaddyPurchaseReceiptDto, UpdatePaddyPurchaseReceiptDto, DTParameter>
{
    /// <summary>
    /// Chốt phiếu: sinh PaddyLot, tạo InboundOrder, cập nhật tồn, ghi công nợ nếu có.
    /// </summary>
    Task<ApiResponse> ConfirmReceiptAsync(int receiptId, int confirmedById);
}
