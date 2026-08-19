using System.Threading.Tasks;
using Backend.Application.DTOs.MillingOrders;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IMillingOrderService : IServiceBase<int, CreateMillingOrderDto, UpdateMillingOrderDto, DTParameter>
{
    Task<ApiResponse> ReserveAsync(int id, ReserveMillingOrderDto dto, int userId);
    Task<ApiResponse> SuggestSourcesAsync(int id);
    /// <summary>W14-J: Bắt đầu lệnh xay với MachineRef bắt buộc và OperatorId tùy chọn.</summary>
    Task<ApiResponse> StartAsync(int id, StartMillingOrderDto dto, int userId);
    Task<ApiResponse> CancelAsync(int id, int userId);

    /// <summary>
    /// Hoàn thành lệnh xay: trừ lúa từ lô đầu vào, sinh lô gạo/phụ phẩm, nhập kho đầu ra.
    /// </summary>
    Task<ApiResponse> CompleteMillingOrderAsync(int orderId, CompleteMillingOrderDto dto, int completedById);

    /// <summary>Gap 2: Lấy các lệnh xay gắn với một đơn bán (điều phối xay-theo-đơn).</summary>
    Task<ApiResponse> GetBySalesOrderAsync(int salesOrderId);
}
