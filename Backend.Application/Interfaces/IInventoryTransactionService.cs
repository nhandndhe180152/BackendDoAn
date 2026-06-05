using System;
using Backend.Application.DTOs.InventoryTransactions;
using Backend.Domain.DTParameters;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IInventoryTransactionService
{
    Task<ApiResponse> GetPagedAsync(InventoryTransactionDTParameters parameters);

    Task<ApiResponse> GetByIdAsync(int id);

    Task<ApiResponse> GetByProductVariantAsync(int productVariantId, int limit = 100);

    Task<ApiResponse> ManualAdjustmentAsync(ManualInventoryAdjustmentDto request);

    Task<ApiResponse> ReceiveStockAsync(StockMovementRequestDto request);

    Task<ApiResponse> DispatchStockAsync(StockMovementRequestDto request);

    Task<ApiResponse> AdjustStockAsync(StockMovementRequestDto request, int newQuantityOnHand);
}
