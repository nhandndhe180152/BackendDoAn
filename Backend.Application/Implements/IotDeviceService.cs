using System;
using System.Security.Cryptography;
using Backend.Application.Constants;
using Backend.Application.DTOs.IotDevices;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

public class IotDeviceService : IIotDeviceService
{
    private readonly IRepositoryBase<IotDevice, int> _iotDeviceBaseRepository;
    private readonly IRepositoryBase<Warehouse, int> _warehouseRepository;
    private readonly IIotDeviceRepository _iotDeviceRepository;

    public IotDeviceService(
        IRepositoryBase<IotDevice, int> iotDeviceBaseRepository,
        IRepositoryBase<Warehouse, int> warehouseRepository,
        IIotDeviceRepository iotDeviceRepository)
    {
        _iotDeviceBaseRepository = iotDeviceBaseRepository;
        _warehouseRepository = warehouseRepository;
        _iotDeviceRepository = iotDeviceRepository;
    }

    public async Task<ApiResponse> CreateAsync(CreateIotDeviceDto obj)
    {
        var deviceCode = obj.DeviceCode.Trim().ToUpperInvariant();

        var isExistingWarehouse = await _warehouseRepository.AnyAsync(x =>
            x.Id == obj.WarehouseId &&
            x.IsActive &&
            !x.IsDeleted);

        if (!isExistingWarehouse)
        {
            return ApiResponse.NotFound();
        }

        var isExistingDeviceCode = await _iotDeviceBaseRepository.AnyAsync(x =>
            x.DeviceCode == deviceCode &&
            !x.IsDeleted);

        if (isExistingDeviceCode)
        {
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.DeviceCode),
                ApiCodeConstants.Common.DuplicatedData);
        }

        // Nếu người dùng không nhập API Key, hệ thống sẽ tự động sinh ngẫu nhiên.
        var plainApiKey = string.IsNullOrWhiteSpace(obj.ApiKey)
            ? GenerateDeviceKey()
            : obj.ApiKey.Trim();

        var entity = obj.ToEntity(plainApiKey);

        await _iotDeviceBaseRepository.CreateAsync(entity);
        await _iotDeviceBaseRepository.SaveChangesAsync();

        var result = new CreateIotDeviceResultDto
        {
            Id = entity.Id,
            DeviceCode = entity.DeviceCode,
            ApiKey = plainApiKey
        };

        return ApiResponse.Created(result, "Tạo thiết bị IoT thành công.");
    }

    public Task<ApiResponse> CreateListAsync(IEnumerable<CreateIotDeviceDto> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> GetAllAsync()
    {
        var data = await _iotDeviceBaseRepository
            .GetAll(true, x => x.Warehouse)
            .OrderByDescending(x => x.CreatedDate)
            .Select(x => new IotDeviceDetailDto
            {
                Id = x.Id,
                WarehouseId = x.WarehouseId,
                WarehouseName = x.Warehouse.Name,
                WarehouseCode = x.Warehouse.Code,
                DeviceName = x.DeviceName,
                DeviceCode = x.DeviceCode,
                DeviceType = x.DeviceType,
                Location = x.Location,
                MqttTopic = x.MqttTopic,
                LastHeartbeat = x.LastHeartbeat,
                IsOnline = x.IsOnline,
                IsActive = x.IsActive,
                CreatedDate = x.CreatedDate,
                LastModifiedDate = x.LastModifiedDate
            })
            .ToListAsync();

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await _iotDeviceRepository.GetDetailByIdAsync(id);

        if (data == null)
        {
            return ApiResponse.NotFound();
        }

        return ApiResponse.Success(data.ToDto());
    }

    public async Task<ApiResponse> GetPagedAsync(DTParameter parameters)
    {
        var data = await _iotDeviceRepository.GetPagedAsync(parameters);

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

    public async Task<ApiResponse> UpdateAsync(UpdateIotDeviceDto obj)
    {
        var entity = await _iotDeviceBaseRepository.GetByIdAsync(obj.Id);

        if (entity == null)
        {
            return ApiResponse.NotFound();
        }

        var isExistingWarehouse = await _warehouseRepository.AnyAsync(x =>
            x.Id == obj.WarehouseId &&
            x.IsActive &&
            !x.IsDeleted);

        if (!isExistingWarehouse)
        {
            return ApiResponse.NotFound();
        }

        var deviceCode = obj.DeviceCode.Trim().ToUpperInvariant();

        var isDuplicatedCode = await _iotDeviceBaseRepository.AnyAsync(x =>
            x.Id != obj.Id &&
            x.DeviceCode == deviceCode &&
            !x.IsDeleted);

        if (isDuplicatedCode)
        {
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.DeviceCode),
                ApiCodeConstants.Common.DuplicatedData);
        }

        obj.ToEntity(entity);

        await _iotDeviceBaseRepository.UpdateAsync(entity);
        await _iotDeviceBaseRepository.SaveChangesAsync();

        return ApiResponse.Success(entity.Id, "Cập nhật thiết bị IoT thành công.");
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateIotDeviceDto> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> UpdateActiveStatusAsync(int id, bool isActive, int? updatedBy)
    {
        var entity = await _iotDeviceBaseRepository.GetByIdAsync(id);

        if (entity == null)
        {
            return ApiResponse.NotFound();
        }

        entity.IsActive = isActive;
        entity.UpdatedBy = updatedBy;
        entity.LastModifiedDate = DateTime.Now;

        await _iotDeviceBaseRepository.UpdateAsync(entity);
        await _iotDeviceBaseRepository.SaveChangesAsync();

        return ApiResponse.Success(entity.Id, isActive ? "Đã kích hoạt thiết bị IoT." : "Đã tạm ngưng thiết bị IoT.");
    }

    /// <summary>
    /// Cấp phát lại mã API Key mới cho thiết bị.
    /// Sử dụng khi thiết bị bị lộ key hoặc cần cấu hình lại.
    /// </summary>
    public async Task<ApiResponse> RegenerateApiKeyAsync(int id, int? updatedBy)
    {
        var entity = await _iotDeviceBaseRepository.GetByIdAsync(id);

        if (entity == null)
        {
            return ApiResponse.NotFound();
        }

        var plainApiKey = GenerateDeviceKey();

        entity.ApiKeyHash = DeviceKeyHelper.HashKey(plainApiKey.Trim());
        entity.UpdatedBy = updatedBy;
        entity.LastModifiedDate = DateTime.Now;

        await _iotDeviceBaseRepository.UpdateAsync(entity);
        await _iotDeviceBaseRepository.SaveChangesAsync();

        var result = new IotDeviceApiKeyDto
        {
            Id = entity.Id,
            DeviceCode = entity.DeviceCode,
            ApiKey = plainApiKey
        };

        return ApiResponse.Success(result, "Tạo lại Device Key thành công. Hãy cập nhật key mới vào ESP32.");
    }

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var entity = await _iotDeviceBaseRepository.GetByIdAsync(id);

        if (entity == null)
        {
            return ApiResponse.NotFound();
        }

        var isDeleted = await _iotDeviceBaseRepository.SoftDeleteAsync(id);

        if (!isDeleted)
        {
            return ApiResponse.BadRequest();
        }

        await _iotDeviceBaseRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }

    public async Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        var isDeleted = await _iotDeviceBaseRepository.SoftDeleteListAsync(objs);

        if (!isDeleted)
        {
            return ApiResponse.BadRequest();
        }

        await _iotDeviceBaseRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }

    private static string GenerateDeviceKey()
    {
        // Dùng RandomNumberGenerator của thư viện mật mã (Cryptography) 
        // để tạo ra chuỗi ngẫu nhiên cực kỳ an toàn, chống việc hacker phỏng đoán (Brute-force/Predict).
        var bytes = RandomNumberGenerator.GetBytes(32);

        // Convert byte sang chuỗi Hex (chữ và số) và thêm tiền tố "stk_" (System token key)
        return "stk_" + Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
