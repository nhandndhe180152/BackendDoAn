using System;
using Backend.Domain.DTParameters;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IInventoryService
{
    Task<ApiResponse> GetPagedAsync(InventoryDTParameters parameters);

    Task<ApiResponse> GetByIdAsync(int id);

    Task<ApiResponse> GetByProductVariantAsync(int productVariantId);

    Task<ApiResponse> GetLowStockAsync(int? warehouseId, int limit = 50);
}
