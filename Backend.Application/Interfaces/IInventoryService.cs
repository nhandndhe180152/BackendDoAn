using System;
using Backend.Domain.DTParameters;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IInventoryService
{
    Task<ApiResponse> GetPagedAsync(InventoryDTParameters parameters);

    /// <summary>Tổng hợp KPI tồn kho theo trạng thái (5 thẻ màn giám sát tồn kho).</summary>
    Task<ApiResponse> GetStockSummaryAsync(InventorySummaryParameters parameters);

    Task<ApiResponse> GetByIdAsync(int id);

    Task<ApiResponse> GetByProductVariantAsync(int productVariantId);

    Task<ApiResponse> GetLowStockAsync(int? warehouseId, int limit = 50);
}
