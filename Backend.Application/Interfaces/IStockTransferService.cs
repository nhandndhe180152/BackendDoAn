using System.Threading.Tasks;
using Backend.Application.DTOs.StockTransfers;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IStockTransferService : IServiceBase<int, CreateStockTransferDto, UpdateStockTransferDto, DTParameter>
{
    Task<ApiResponse> GetSummaryAsync();
    Task<ApiResponse> GetSourceBagsAsync(int fromWarehouseId, int fromLocationId, int? productVariantId);
    Task<ApiResponse> GetDestinationSuggestionsAsync(int toWarehouseId, int productVariantId, decimal weightKg);
    Task<ApiResponse> GetQuarantineSuggestionsAsync(int fromWarehouseId, int productVariantId, decimal weightKg);
    Task<ApiResponse> DispatchAsync(int id, int dispatchedById);
    Task<ApiResponse> ReceiveAsync(int id, int receivedById);
    Task<ApiResponse> CancelAsync(int id, string? reason, int cancelledById);
}
