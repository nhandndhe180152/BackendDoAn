using System.Threading.Tasks;
using Backend.Application.DTOs.StockTakes;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IStockTakeService : IServiceBase<int, CreateStockTakeDto, UpdateStockTakeDto, DTParameter>
{
    Task<ApiResponse> GetSummaryAsync();
    Task<ApiResponse> GetThresholdsAsync();
    Task<ApiResponse> SaveCountsAsync(int id, SaveStockTakeCountsDto dto, int userId);
    Task<ApiResponse> SubmitAsync(int id, SubmitStockTakeDto dto, int userId);
    Task<ApiResponse> ApproveAsync(int id, string? approveNote, int userId);
    Task<ApiResponse> RejectAsync(int id, string reason, int userId);

    /// <summary>Quét QR một bao trong lúc kiểm kê (luôn trả 200 kèm lý do nếu không khớp).</summary>
    Task<ApiResponse> ScanBagAsync(int id, ScanStockTakeBagDto dto, int userId);

    /// <summary>Nguồn dropdown chọn phạm vi kiểm kê: khu / cột / lô đang có bao.</summary>
    Task<ApiResponse> GetScopeOptionsAsync(int warehouseId, bool? quarantineOnly);

    /// <summary>Quét QR dán trên khu/cột hoặc lô để chọn phạm vi kiểm kê.</summary>
    Task<ApiResponse> ResolveScopeQrAsync(string qrCode, int? warehouseId);

    /// <summary>Gợi ý vị trí đích cho bao chuyển cách ly hoặc rút khỏi cách ly.</summary>
    Task<ApiResponse> GetBagTargetSuggestionsAsync(int stockTakeId, int stockTakeItemBagId);
}
