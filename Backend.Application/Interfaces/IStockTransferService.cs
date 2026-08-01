using System.Threading.Tasks;
using Backend.Application.DTOs.StockTransfers;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IStockTransferService : IServiceBase<int, CreateStockTransferDto, UpdateStockTransferDto, DTParameter>
{
    Task<ApiResponse> GetSummaryAsync();
    Task<ApiResponse> DispatchAsync(int id, int dispatchedById);
    Task<ApiResponse> ReceiveAsync(int id, int receivedById);
    Task<ApiResponse> CancelAsync(int id, string? reason, int cancelledById);
}
