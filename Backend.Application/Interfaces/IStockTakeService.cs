using System.Threading.Tasks;
using Backend.Application.DTOs.StockTakes;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IStockTakeService : IServiceBase<int, CreateStockTakeDto, UpdateStockTakeDto, DTParameter>
{
    Task<ApiResponse> GetSummaryAsync();
    Task<ApiResponse> GetThresholdsAsync();
    Task<ApiResponse> SaveCountsAsync(int id, SaveStockTakeCountsDto dto, int userId);

    /// <summary>Tra mã QR bao khi đang kiểm kê (dùng cho màn quét trên mobile).</summary>
    Task<ApiResponse> ScanBagAsync(int stockTakeId, ScanStockTakeBagDto dto);
    Task<ApiResponse> SubmitAsync(int id, SubmitStockTakeDto dto, int userId);
    Task<ApiResponse> ApproveAsync(int id, string? approveNote, int userId);
    Task<ApiResponse> RejectAsync(int id, string reason, int userId);
}
