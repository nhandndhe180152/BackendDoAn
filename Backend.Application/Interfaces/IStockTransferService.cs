using System.Threading.Tasks;
using Backend.Application.DTOs.StockTransfers;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IStockTransferService : IServiceBase<int, CreateStockTransferDto, UpdateStockTransferDto, DTParameter>
{
    /// <summary>
    /// Xác nhận điều chuyển: xuất kho nguồn, nhập kho đích, cập nhật PaddyLot.
    /// </summary>
    Task<ApiResponse> ConfirmTransferAsync(int id, int confirmedById);
}
