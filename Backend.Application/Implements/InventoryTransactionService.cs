using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.InventoryTransactions;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.DTParameters;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Microsoft.AspNetCore.Http;

namespace Backend.Application.Implements;

public class InventoryTransactionService : IInventoryTransactionService
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IInventoryTransactionRepository _inventoryTransactionRepository;
    private readonly IProductVariantRepository _productVariantRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public InventoryTransactionService(
        IInventoryRepository inventoryRepository,
        IInventoryTransactionRepository inventoryTransactionRepository,
        IProductVariantRepository productVariantRepository,
        IHttpContextAccessor httpContextAccessor)
    {
        _inventoryRepository = inventoryRepository;
        _inventoryTransactionRepository = inventoryTransactionRepository;
        _productVariantRepository = productVariantRepository;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<ApiResponse> GetPagedAsync(InventoryTransactionDTParameters parameters)
    {
        var result = await _inventoryTransactionRepository.GetPagedAsync(parameters);
        return ApiResponse.Success(result);
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await _inventoryTransactionRepository.GetByIdDetailAsync(id);

        if (data == null)
        {
            return ApiResponse.NotFound();
        }

        return ApiResponse.Success(data.ToDto());
    }

    public async Task<ApiResponse> GetByProductVariantAsync(int productVariantId, int limit = 100)
    {
        if (productVariantId <= 0)
        {
            return ApiResponse.BadRequest();
        }

        limit = limit <= 0 ? 100 : Math.Min(limit, 500);

        var data = await _inventoryTransactionRepository.GetByProductVariantAsync(productVariantId, limit);
        return ApiResponse.Success(data.Select(x => x.ToDto()).ToList());
    }

    /// <summary>
    /// Xử lý nghiệp vụ tồn kho, bao gồm nhập/xuất/điều chỉnh tồn và ghi nhận lịch sử InventoryTransaction.
    /// </summary>
    /// <param name="request">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <returns>Kết quả xử lý của hàm, thường là dữ liệu, ApiResponse, IActionResult hoặc trạng thái thao tác.</returns>
    public async Task<ApiResponse> ManualAdjustmentAsync(ManualInventoryAdjustmentDto request)
    {
        if (request.ProductVariantId <= 0)
        {
            return ApiResponse.BadRequest();
        }

        if (request.WarehouseId <= 0)
        {
            return ApiResponse.BadRequest();
        }

        if (!request.NewQuantityOnHand.HasValue && !request.AdjustmentQuantity.HasValue)
        {
            return ApiResponse.BadRequest();
        }

        if (request.NewQuantityOnHand.HasValue && request.NewQuantityOnHand.Value < 0)
        {
            return ApiResponse.BadRequest();
        }

        if (request.AdjustmentQuantity.HasValue && request.AdjustmentQuantity.Value == 0)
        {
            return ApiResponse.BadRequest();
        }

        // Tìm dòng tồn kho hiện tại theo SKU + warehouse + location.
        // Đây là tổ hợp quyết định một vị trí tồn kho cụ thể trong StockLite.
        var inventory = await _inventoryRepository.GetByVariantWarehouseLocationAsync(
            request.ProductVariantId,
            request.WarehouseId,
            request.LocationId);

        var currentQuantity = inventory?.QuantityOnHand ?? 0;

        var newQuantity = request.NewQuantityOnHand
            ?? currentQuantity + request.AdjustmentQuantity!.Value;

        if (newQuantity < 0)
        {
            return ApiResponse.BadRequest();
        }

        var movement = new StockMovementRequestDto
        {
            ProductVariantId = request.ProductVariantId,
            WarehouseId = request.WarehouseId,
            LocationId = request.LocationId,
            Quantity = Math.Abs(newQuantity - currentQuantity),
            ReferenceType = InventoryReferenceTypeConstants.Manual,
            Note = request.Reason
        };

        return await ApplyMovementAsync(
            movement,
            InventoryTransactionTypeConstants.ManualAdjust,
            newQuantity);
    }

    /// <summary>
    /// Xử lý nghiệp vụ tồn kho, bao gồm nhập/xuất/điều chỉnh tồn và ghi nhận lịch sử InventoryTransaction.
    /// </summary>
    /// <param name="request">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <returns>Kết quả xử lý của hàm, thường là dữ liệu, ApiResponse, IActionResult hoặc trạng thái thao tác.</returns>
    public async Task<ApiResponse> ReceiveStockAsync(StockMovementRequestDto request)
    {
        return await ApplyMovementAsync(
            request,
            InventoryTransactionTypeConstants.Import,
            null);
    }

    /// <summary>
    /// Xử lý nghiệp vụ tồn kho, bao gồm nhập/xuất/điều chỉnh tồn và ghi nhận lịch sử InventoryTransaction.
    /// </summary>
    /// <param name="request">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <returns>Kết quả xử lý của hàm, thường là dữ liệu, ApiResponse, IActionResult hoặc trạng thái thao tác.</returns>
    public async Task<ApiResponse> DispatchStockAsync(StockMovementRequestDto request)
    {
        return await ApplyMovementAsync(
            request,
            InventoryTransactionTypeConstants.Export,
            null);
    }

    /// <summary>
    /// Xử lý nghiệp vụ tồn kho, bao gồm nhập/xuất/điều chỉnh tồn và ghi nhận lịch sử InventoryTransaction.
    /// </summary>
    /// <param name="request">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <param name="newQuantityOnHand">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <returns>Kết quả xử lý của hàm, thường là dữ liệu, ApiResponse, IActionResult hoặc trạng thái thao tác.</returns>
    public async Task<ApiResponse> AdjustStockAsync(StockMovementRequestDto request, int newQuantityOnHand)
    {
        return await ApplyMovementAsync(
            request,
            InventoryTransactionTypeConstants.StockTakeAdjust,
            newQuantityOnHand);
    }

    /// <summary>
    /// Áp dụng logic nghiệp vụ chính lên dữ liệu hiện tại, thường bao gồm validate, cập nhật entity và lưu transaction.
    /// </summary>
    /// <param name="request">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <param name="transactionType">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <param name="newQuantityOnHand">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <returns>Kết quả xử lý của hàm, thường là dữ liệu, ApiResponse, IActionResult hoặc trạng thái thao tác.</returns>
    private async Task<ApiResponse> ApplyMovementAsync(
        StockMovementRequestDto request,
        string transactionType,
        int? newQuantityOnHand)
    {
        // Mở database transaction để việc cập nhật Inventory và ghi InventoryTransaction luôn đồng bộ.
        // Nếu một bước lỗi thì rollback để tránh tồn kho đã đổi nhưng lịch sử chưa ghi, hoặc ngược lại.
        await using var dbTransaction = await _inventoryRepository.BeginTransactionAsync();

        try
        {
            if (request.ProductVariantId <= 0)
            {
                return ApiResponse.BadRequest();
            }

            if (request.WarehouseId <= 0)
            {
                return ApiResponse.BadRequest();
            }

            if (request.Quantity <= 0 && !newQuantityOnHand.HasValue)
            {
                return ApiResponse.BadRequest();
            }

            var productVariant = await _productVariantRepository.GetActiveByIdAsync(request.ProductVariantId);
            if (productVariant == null)
            {
                return ApiResponse.NotFound();
            }

            // Chuẩn hóa loại giao dịch để tránh lỗi do client/service truyền chữ hoa, chữ thường hoặc alias khác nhau.
            transactionType = InventoryTransactionTypeConstants.Normalize(transactionType);
            var referenceType = InventoryReferenceTypeConstants.Normalize(request.ReferenceType);

            var now = DateTime.Now;
            var currentUserId = _httpContextAccessor.HttpContext?.GetCurrentUserId();

            // Tìm dòng tồn kho hiện tại theo SKU + warehouse + location.
            // Đây là tổ hợp quyết định một vị trí tồn kho cụ thể trong StockLite.
            var inventory = await _inventoryRepository.GetByVariantWarehouseLocationAsync(
                request.ProductVariantId,
                request.WarehouseId,
                request.LocationId);

            // Nếu chưa có dòng Inventory thì chỉ cho phép tạo mới khi nhập kho/điều chỉnh hợp lệ.
            // Xuất kho từ một dòng tồn kho chưa tồn tại là nghiệp vụ sai nên phải rollback.
            if (inventory == null)
            {
                if (transactionType == InventoryTransactionTypeConstants.Export)
                {
                    await _inventoryRepository.RollbackTransactionAsync();
                    return ApiResponse.BadRequest();
                }

                inventory = new Inventory
                {
                    WarehouseId = request.WarehouseId,
                    LocationId = request.LocationId,
                    ProductVariantId = request.ProductVariantId,
                    InboundOrderId = referenceType == InventoryReferenceTypeConstants.InboundOrder
                        ? request.ReferenceId
                        : null,
                    CostPrice = request.CostPrice ?? productVariant.CostPrice,
                    QuantityOnHand = 0,
                    QuantityReserved = 0,
                    CreatedDate = now
                };

                await _inventoryRepository.CreateAsync(inventory);
                await _inventoryRepository.SaveChangesAsync();
            }

            var beforeQuantity = inventory.QuantityOnHand;
            int afterQuantity;
            int transactionQuantity;

            // Tính số lượng trước/sau theo từng loại giao dịch:
            // - Import: cộng tồn
            // - Export: trừ tồn và kiểm tra available quantity
            // - Adjustment: đặt lại số lượng thực tế sau kiểm kê/điều chỉnh thủ công
            switch (transactionType)
            {
                case InventoryTransactionTypeConstants.Import:
                    transactionQuantity = request.Quantity;
                    afterQuantity = beforeQuantity + request.Quantity;
                    break;

                case InventoryTransactionTypeConstants.Export:
                    var availableQuantity = inventory.QuantityOnHand - inventory.QuantityReserved;

                    if (availableQuantity < request.Quantity)
                    {
                        await _inventoryRepository.RollbackTransactionAsync();
                        return ApiResponse.BadRequest();
                    }

                    transactionQuantity = -request.Quantity;
                    afterQuantity = beforeQuantity - request.Quantity;
                    break;

                case InventoryTransactionTypeConstants.StockTakeAdjust:
                case InventoryTransactionTypeConstants.ManualAdjust:
                    if (!newQuantityOnHand.HasValue)
                    {
                        await _inventoryRepository.RollbackTransactionAsync();
                        return ApiResponse.BadRequest();
                    }

                    if (newQuantityOnHand.Value < 0)
                    {
                        await _inventoryRepository.RollbackTransactionAsync();
                        return ApiResponse.BadRequest();
                    }

                    afterQuantity = newQuantityOnHand.Value;
                    transactionQuantity = afterQuantity - beforeQuantity;
                    break;

                default:
                    await _inventoryRepository.RollbackTransactionAsync();
                    return ApiResponse.BadRequest();
            }

            inventory.QuantityOnHand = afterQuantity;
            inventory.CostPrice = request.CostPrice ?? inventory.CostPrice;
            inventory.LastModifiedDate = now;

            if (transactionType == InventoryTransactionTypeConstants.StockTakeAdjust)
            {
                inventory.LastStockTakeDate = now;
            }

            await _inventoryRepository.UpdateAsync(inventory);

            // Ghi một dòng lịch sử giao dịch tồn kho sau khi đã tính before/after quantity.
            // Dữ liệu này phục vụ audit trail, báo cáo stock movement và truy vết sai lệch.
            var inventoryTransaction = new InventoryTransaction
            {
                InventoryId = inventory.Id,
                WarehouseId = request.WarehouseId,
                LocationId = request.LocationId,
                ProductVariantId = request.ProductVariantId,
                TransactionType = transactionType,
                ReferenceType = referenceType,
                ReferenceId = request.ReferenceId,
                ReferenceItemId = request.ReferenceItemId,
                Quantity = transactionQuantity,
                BeforeQuantity = beforeQuantity,
                AfterQuantity = afterQuantity,
                WeightKg = request.WeightKg,
                IotWeightLogId = request.IotWeightLogId,
                Note = request.Note,
                CreatedDate = now,
                CreatedBy = currentUserId
            };

            await _inventoryTransactionRepository.CreateAsync(inventoryTransaction);
            await _inventoryTransactionRepository.SaveChangesAsync();
            await _inventoryRepository.EndTransactionAsync();

            var savedInventory = await _inventoryRepository.GetByIdDetailAsync(inventory.Id) ?? inventory;
            var savedTransaction = await _inventoryTransactionRepository.GetByIdDetailAsync(inventoryTransaction.Id)
                                   ?? inventoryTransaction;

            return ApiResponse.Success(
                new StockMovementResultDto
                {
                    Inventory = savedInventory.ToDto(),
                    Transaction = savedTransaction.ToDto()
                },
                "Cập nhật tồn kho và ghi InventoryTransaction thành công.");
        }
        catch (Exception)
        {
            await _inventoryRepository.RollbackTransactionAsync();
            return ApiResponse.InternalServerError();
        }
    }
}
