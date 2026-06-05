using System;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.DTParameters;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;

namespace Backend.Application.Implements;

public class InventoryService : IInventoryService
{
    private readonly IInventoryRepository _inventoryRepository;

    public InventoryService(IInventoryRepository inventoryRepository)
    {
        _inventoryRepository = inventoryRepository;
    }

    public async Task<ApiResponse> GetPagedAsync(InventoryDTParameters parameters)
    {
        var result = await _inventoryRepository.GetPagedAsync(parameters);
        return ApiResponse.Success(result);
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await _inventoryRepository.GetByIdDetailAsync(id);

        if (data == null)
        {
            return ApiResponse.NotFound();
        }

        return ApiResponse.Success(data.ToDto());
    }

    public async Task<ApiResponse> GetByProductVariantAsync(int productVariantId)
    {
        if (productVariantId <= 0)
        {
            return ApiResponse.BadRequest();
        }

        var data = await _inventoryRepository.GetByProductVariantAsync(productVariantId);
        return ApiResponse.Success(data.Select(x => x.ToDto()).ToList());
    }

    /// <summary>
    /// Xử lý nghiệp vụ tồn kho, bao gồm nhập/xuất/điều chỉnh tồn và ghi nhận lịch sử InventoryTransaction.
    /// </summary>
    /// <param name="warehouseId">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <param name="limit">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <returns>Kết quả xử lý của hàm, thường là dữ liệu, ApiResponse, IActionResult hoặc trạng thái thao tác.</returns>
    public async Task<ApiResponse> GetLowStockAsync(int? warehouseId, int limit = 50)
    {
        limit = limit <= 0 ? 50 : Math.Min(limit, 200);

        var data = await _inventoryRepository.GetLowStockAsync(warehouseId, limit);
        return ApiResponse.Success(data.Select(x => x.ToDto()).ToList());
    }
}
