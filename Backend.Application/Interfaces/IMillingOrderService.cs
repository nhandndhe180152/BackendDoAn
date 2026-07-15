using System.Threading.Tasks;
using Backend.Application.DTOs.MillingOrders;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IMillingOrderService : IServiceBase<int, CreateMillingOrderDto, UpdateMillingOrderDto, DTParameter>
{
    /// <summary>
    /// Hoàn thành lệnh xay: trừ lúa từ lô đầu vào, sinh lô gạo/phụ phẩm, nhập kho đầu ra.
    /// </summary>
    Task<ApiResponse> CompleteMillingOrderAsync(int orderId, int completedById);
}
