using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.StockAlertConfigs;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

/// <summary>
/// Nghiệp vụ cấu hình ngưỡng cảnh báo tồn thấp theo kho/SKU (SCR-20). Đầu vào cho JOB-01 Low Stock.
/// </summary>
public class StockAlertConfigService : IStockAlertConfigService
{
    private readonly IStockAlertConfigRepository _stockAlertConfigRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IProductVariantRepository _productVariantRepository;

    public StockAlertConfigService(
        IStockAlertConfigRepository stockAlertConfigRepository,
        IWarehouseRepository warehouseRepository,
        IProductVariantRepository productVariantRepository)
    {
        _stockAlertConfigRepository = stockAlertConfigRepository;
        _warehouseRepository = warehouseRepository;
        _productVariantRepository = productVariantRepository;
    }

    public async Task<ApiResponse> CreateAsync(CreateStockAlertConfigDto obj)
    {
        var validation = await ValidateReferencesAsync(obj);
        if (validation != null) return validation;

        var isDuplicated = await _stockAlertConfigRepository.AnyAsync(x =>
            x.WarehouseId == obj.WarehouseId &&
            x.ProductVariantId == obj.ProductVariantId &&
            x.IsActive);

        if (isDuplicated)
            return ApiResponse.UnprocessableEntity(
                "Đã tồn tại cấu hình ngưỡng tồn đang áp dụng cho kho/SKU này.",
                ApiCodeConstants.Common.DuplicatedData);

        var model = obj.ToEntity();

        await _stockAlertConfigRepository.CreateAsync(model);
        await _stockAlertConfigRepository.SaveChangesAsync();

        return ApiResponse.Created(model.Id, "Tạo cấu hình cảnh báo tồn thấp thành công.");
    }

    public Task<ApiResponse> CreateListAsync(IEnumerable<CreateStockAlertConfigDto> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> GetAllAsync()
    {
        var entities = await _stockAlertConfigRepository
            .FindByCondition(x => !x.IsDeleted)
            .Include(x => x.Warehouse)
            .Include(x => x.ProductVariant)
                .ThenInclude(pv => pv!.Product)
            .OrderByDescending(x => x.CreatedDate)
            .ToListAsync();

        var data = entities.Select(x => x.ToDto()).ToList();

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await _stockAlertConfigRepository
            .FindByCondition(x => x.Id == id && !x.IsDeleted)
            .Include(x => x.Warehouse)
            .Include(x => x.ProductVariant)
                .ThenInclude(pv => pv!.Product)
            .FirstOrDefaultAsync();

        if (data == null)
            return ApiResponse.NotFound();

        return ApiResponse.Success(data.ToDto());
    }

    public async Task<ApiResponse> GetPagedAsync(DTParameter parameters)
    {
        var data = await _stockAlertConfigRepository.GetPagedAsync(parameters);
        return ApiResponse.Success(data);
    }

    public Task<ApiResponse> GetPagedAsync(SearchQuery query)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> GetPagedAsync<T>(AdvancedSearchQuery<T> query)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> UpdateAsync(UpdateStockAlertConfigDto obj)
    {
        var existData = await _stockAlertConfigRepository.GetByIdAsync(obj.Id);
        if (existData == null || existData.IsDeleted)
            return ApiResponse.NotFound();

        var validation = await ValidateReferencesAsync(obj);
        if (validation != null) return validation;

        var isDuplicated = await _stockAlertConfigRepository.AnyAsync(x =>
            x.Id != obj.Id &&
            x.WarehouseId == obj.WarehouseId &&
            x.ProductVariantId == obj.ProductVariantId &&
            x.IsActive);

        if (isDuplicated)
            return ApiResponse.UnprocessableEntity(
                "Đã tồn tại cấu hình ngưỡng tồn đang áp dụng cho kho/SKU này.",
                ApiCodeConstants.Common.DuplicatedData);

        obj.ToEntity(existData);

        await _stockAlertConfigRepository.UpdateAsync(existData);
        await _stockAlertConfigRepository.SaveChangesAsync();

        return ApiResponse.Success(existData.Id, "Cập nhật cấu hình cảnh báo tồn thấp thành công.");
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateStockAlertConfigDto> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var isDeleted = await _stockAlertConfigRepository.SoftDeleteAsync(id);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _stockAlertConfigRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    public async Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        var isDeleted = await _stockAlertConfigRepository.SoftDeleteListAsync(objs);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _stockAlertConfigRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    /// <summary>Kiểm tra kho và SKU tham chiếu có tồn tại. Trả về null nếu hợp lệ.</summary>
    private async Task<ApiResponse?> ValidateReferencesAsync(CreateStockAlertConfigDto obj)
    {
        var isWarehouseExisting = await _warehouseRepository.AnyAsync(x => x.Id == obj.WarehouseId);
        if (!isWarehouseExisting)
            return ApiResponse.NotFound("Không tìm thấy kho tương ứng.", ApiCodeConstants.Common.NotFound);

        if (obj.ProductVariantId.HasValue)
        {
            var isVariantExisting = await _productVariantRepository.AnyAsync(x => x.Id == obj.ProductVariantId.Value);
            if (!isVariantExisting)
                return ApiResponse.NotFound("Không tìm thấy SKU (biến thể sản phẩm) tương ứng.", ApiCodeConstants.Common.NotFound);
        }

        return null;
    }
}
