using System.Threading;
using System.Threading.Tasks;
using Backend.Application.DTOs.Putaway;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IPutawaySuggestionService
{
    /// <summary>
    /// Gợi ý vị trí xếp kho dựa trên các quy tắc trọng số.
    /// Trả về danh sách gợi ý hoặc phương án chia nhỏ lô nếu cần.
    /// </summary>
    Task<ApiResponse> GetSuggestionsAsync(GetPutawaySuggestionsRequest request, CancellationToken cancellationToken);

    /// <summary>Lấy cấu hình quy tắc gợi ý (ưu tiên cấu hình kho trước toàn hệ thống).</summary>
    Task<ApiResponse> GetConfigAsync(int? warehouseId, CancellationToken cancellationToken);

    /// <summary>Cập nhật cấu hình quy tắc gợi ý vị trí.</summary>
    Task<ApiResponse> UpdateConfigAsync(int id, UpdatePutawayRuleConfigDto dto, CancellationToken cancellationToken);

    /// <summary>
    /// Xác nhận thực tế đưa hàng vào vị trí lưu kho.
    /// Chạy trong 1 Transaction, kiểm tra tranh chấp (UPDATE Location) và cập nhật tồn kho.
    /// </summary>
    Task<ApiResponse> ConfirmStoreInAsync(string referenceType, int referenceId, ConfirmStoreInRequest request, CancellationToken cancellationToken);
}
