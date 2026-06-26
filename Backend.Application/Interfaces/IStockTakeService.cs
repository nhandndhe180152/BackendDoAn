using System.Threading.Tasks;
using Backend.Application.DTOs.StockTakes;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IStockTakeService : IServiceBase<int, CreateStockTakeDto, UpdateStockTakeDto, DTParameter>
{
    Task<ApiResponse> ApproveAsync(int id, int userId);
    Task<ApiResponse> RejectAsync(int id, string reason, int userId);
}
